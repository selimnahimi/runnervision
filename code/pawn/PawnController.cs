using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MyGame;

public class PawnController : EntityComponent<Pawn>
{
	public int StepSize => 26;
	public int GroundAngle => 70;
	public int JumpSpeed => 300;
	public float Gravity => 800f;
	public float StartingSpeed => 600f;
	public float MaxSpeed => 1700f;
	public float SpeedGrowthRate => 5.0f;
	public float SpeedShrinkRate => 50.0f;
	public float Friction => 3.0f;
	public float SharpTurnAngle => 50f;
	public float Acceleration => 0.02f;
	public int Wallrunning { get; set; }
	public int Dashing { get; set; }
	public bool Noclipping { get; set; }
	public bool UnlimitedSprint { get; set; }
	public int Vaulting { get; set; }

	private float CurrentMaxSpeed { get; set; }
	private float TimeSinceLastFootstep { get; set; }
	private float TimeSinceLastFootstepRelease { get; set; }
	private float TimeSinceDash { get; set; }
	private Vector3 VaultTargetPos { get; set; }
	private Vector3 VaultStartPos { get; set; }

	private float bezierCounter = 0f;
	private float vaultSpeed = 0f;

	HashSet<string> ControllerEvents = new( StringComparer.OrdinalIgnoreCase );

	public bool Grounded => Entity.GroundEntity.IsValid();

	public PawnController()
	{
		CurrentMaxSpeed = StartingSpeed;
	}

	public void Simulate( IClient cl )
	{
		ControllerEvents.Clear();

		if ( Noclipping )
		{
			HandleNoclipping();
			return;
		}

		if ( Vaulting != 0 )
		{
			UpdateVault();
			return;
		}

		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles.WithPitch( 0 );
		var moveVector = Rotation.From( angles ) * movement * CurrentMaxSpeed;
		var groundEntity = CheckForGround();

		if ( moveVector.LengthSquared != 0 )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( MaxSpeed, SpeedGrowthRate );
		}
		else
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, SpeedShrinkRate );
		}

		CheckForSharpTurn( moveVector );

		if ( groundEntity.IsValid() )
		{
			if ( !Grounded )
			{
				Sound.FromWorld( "concretefootstepland", Entity.Position + Vector3.Down * 10f );

				Entity.Velocity = Entity.Velocity.WithZ( 0 );
				AddEvent( "grounded" );
				Wallrunning = 0;
			}

			Entity.Velocity = Accelerate( Entity.Velocity, moveVector.Normal, moveVector.Length, CurrentMaxSpeed, Acceleration );
			Entity.Velocity = ApplyFriction( Entity.Velocity, Friction );
		}
		else
		{
			Entity.Velocity += Vector3.Down * (IsWallRunning() ? Gravity * 0.75f : Gravity ) * Time.Delta;
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( !Input.Down("forward") && ( Input.Down( "left" ) || Input.Down( "right" ) ) )
			{
				if ( TimeSinceDash > 1.0f )
				{
					var isLeft = Input.Down( "left" );

					Dashing = isLeft ? 1 : 2;

					Entity.ApplyAbsoluteImpulse( ( isLeft ? Entity.Rotation.Left : Entity.Rotation.Right ) * 300f );
					CurrentMaxSpeed += 200f;

					TimeSinceDash = 0.0f;
				}
			}
			else
			{
				TryVaulting();
				DoJump();
			}

			if ( IsWallRunning() )
			{
				Entity.Velocity *= 0.5f;
				Entity.ApplyAbsoluteImpulse( Entity.Rotation.Forward * 250f + Entity.Rotation.Up * 100f );
				Wallrunning = 0;
			}
			else if ( CheckForWallLeft() )
			{
				Wallrunning = 1;
			}
			else if ( CheckForWallRight() )
			{
				Wallrunning = 2;
			}
		}

		TimeSinceDash += Time.Delta;

		if ( Dashing != 0 && TimeSinceDash > 1.0f )
			Dashing = 0;

		var mh = new MoveHelper( Entity.Position, Entity.Velocity );
		mh.Trace = mh.Trace.Size( Entity.Hull ).Ignore( Entity );
		
		if ( mh.TryMoveWithStep( Time.Delta, StepSize ) > 0 )
		{
			if ( Grounded )
			{
				mh.Position = StayOnGround( mh.Position );
			}
			Entity.Position = mh.Position;
			Entity.Velocity = mh.Velocity;
		}

		Entity.GroundEntity = groundEntity;

		if ( UnlimitedSprint )
			CurrentMaxSpeed = MaxSpeed;

		DebugOverlay.ScreenText( CurrentMaxSpeed.ToString() );

		// DebugOverlay.Line( Entity.Position + Vector3.Up * 50f, Entity.Position + Vector3.Up * 50f + Entity.Rotation.Left * 30f + Entity.Rotation.Forward * 15f );
		// DebugOverlay.Line( Entity.Position + Vector3.Up * 50f, Entity.Position + Vector3.Up * 50f + Entity.Rotation.Right * 30f + Entity.Rotation.Forward * 15f );

		/*DebugOverlay.Box(
			mins: Vector3.Up * 50f + Vector3.Forward * 50f + Vector3.Left * 25f,
			maxs: Vector3.Up * 40f + Vector3.Right * 25f + Vector3.Forward * 25f,
			rotation: Entity.Rotation,
			position: Entity.Position,
			color: Color.Red
		); */

		/*DebugOverlay.Box(
			mins: Vector3.Up * 80f + Vector3.Forward * 50f + Vector3.Left * 25f,
			maxs: Vector3.Up * 60f + Vector3.Right * 25f + Vector3.Forward * 25f,
			rotation: Entity.Rotation,
			position: Entity.Position,
			color: Color.Green
		);*/

		/*DebugOverlay.Line(
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 40f + Entity.Rotation.Left * 25f,
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 40f + Entity.Rotation.Right * 25f,
			Color.Red
		);

		var traceBottom = Trace.Ray(
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 40f + Entity.Rotation.Left * 25f,
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 40f + Entity.Rotation.Right * 25f
		);

		DebugOverlay.Line(
			Entity.Position + Entity.Rotation.Up * 70f + Entity.Rotation.Forward * 40f + Entity.Rotation.Left * 25f,
			Entity.Position + Entity.Rotation.Up * 70f + Entity.Rotation.Forward * 40f + Entity.Rotation.Right * 25f,
			Color.Green
		);

		var traceTop = Trace.Ray(
			Entity.Position + Entity.Rotation.Up * 70f + Entity.Rotation.Forward * 40f + Entity.Rotation.Left * 25f,
			Entity.Position + Entity.Rotation.Up * 70f + Entity.Rotation.Forward * 40f + Entity.Rotation.Right * 25f
		);

		DebugOverlay.Line(
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 60f + Entity.Rotation.Left * 25f,
			Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 60f + Entity.Rotation.Right * 25f,
			Color.Blue
		);*/

		/*float speed = Entity.Velocity.Length;
		float rayDistance = Math.Max((speed / 500) * 60f, 40f);

		DebugOverlay.Line(
			start: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 60f,
			end: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 20f,
			color: Color.Red
		);

		DebugOverlay.Line(
			start: Entity.Position + Entity.Rotation.Up * 30f,
			end: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 30f,
			color: Color.Red
		);

		DebugOverlay.Sphere(
			Entity.Position + Entity.Rotation.Forward * (rayDistance + 30f) + Entity.Rotation.Up * 30f,
			15f,
			Color.Blue
		);

		DebugOverlay.Sphere(
			Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 70f,
			15f,
			Color.Green
		);

		DebugOverlay.Line(
			start: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 70f,
			end: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 40f,
			color: Color.Yellow
		);*/

		DebugOverlay.ScreenText( Vaulting.ToString(), 1 );

		FootstepWizard();
	}

	// Source: https://programmerbay.com/c-program-to-draw-bezier-curve-using-4-control-points/
	Vector3 BezierApproach( Vector3 start, Vector3 end, float curveSize, float t )
	{
		var cp1 = start + Vector3.Up * curveSize;
		var cp2 = end + Vector3.Up * curveSize;
		float x = (float)(Math.Pow( 1 - t, 3 ) * start.x + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.x + 3 * t * t * (1 - t) * cp2.x + Math.Pow( t, 3 ) * end.x);
		float y = (float)(Math.Pow( 1 - t, 3 ) * start.y + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.y + 3 * t * t * (1 - t) * cp2.y + Math.Pow( t, 3 ) * end.y);
		float z = (float)(Math.Pow( 1 - t, 3 ) * start.z + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.z + 3 * t * t * (1 - t) * cp2.z + Math.Pow( t, 3 ) * end.z);

		return new Vector3( x, y, z );
	}

	void UpdateVault()
	{
		Vector3 pos = BezierApproach(
			start: VaultStartPos,
			end: VaultTargetPos,
			curveSize: 30f,
			t: bezierCounter
		);

		bezierCounter += (vaultSpeed/100) * Time.Delta; 
		Log.Info( Time.Delta );

		Log.Info( "pos:" + pos );
		Log.Info( "counter: " + bezierCounter );

		Entity.Position = Entity.Position.LerpTo( pos, 0.5f );

		if ( Entity.Position.AlmostEqual( VaultTargetPos, 7f ) || bezierCounter >= 1.0f )
			Vaulting = 0;
	}

	void TryVaulting()
	{
		float speed = Entity.Velocity.Length;
		float rayDistance = Math.Max( (speed / 500) * 60f, 40f );

		var traceFront = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 20f,
			to: Entity.Position + Entity.Rotation.Forward * rayDistance * 1.5f + Entity.Rotation.Up * 20f
		).Run();

		var traceTop = Trace.Sphere(
			from: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 70f,
			to: Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 70f,
			radius: 15f
		).Run();

		Log.Info( "Front hit: " + traceFront.Hit );
		Log.Info( "Top hit: " + traceTop.Hit );

		if ( !traceFront.Hit || traceTop.Hit )
			return;

		DebugOverlay.Line(
			start: Entity.Position + Entity.Rotation.Forward * traceFront.Distance + Entity.Rotation.Up * 60f,
			end: Entity.Position + Entity.Rotation.Forward * traceFront.Distance + Entity.Rotation.Up,
			color: Color.Blue,
			duration: 1f
		);

		var traceGround = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Forward * (traceFront.Distance + 3) + Entity.Rotation.Up * 60f,
			to: Entity.Position + Entity.Rotation.Forward * (traceFront.Distance + 3) + Entity.Rotation.Up
		).Run();

		Log.Info( traceGround.HitPosition );

		if ( !traceGround.Hit )
			return;

		var traceBehind = Trace.Sphere(
			from: Entity.Position + Entity.Rotation.Forward * (rayDistance + 50f) + Entity.Rotation.Up * 30f,
			to: Entity.Position + Entity.Rotation.Forward * (rayDistance + 50f) + Entity.Rotation.Up * 30f,
			radius: 15f
		).Run();

		VaultStartPos = Entity.Position;
		bezierCounter = 0f;

		if ( !traceBehind.Hit )
		{
			// Vault over
			VaultTargetPos = Entity.Position + Entity.Rotation.Forward * (rayDistance + 60f);
			Vaulting = 2;
		}
		else
		{
			// Vault onto
			Vaulting = 1;
			VaultTargetPos = traceGround.HitPosition + Vector3.Up * 13f;
		}

		var vaultDirection = (VaultTargetPos - Entity.Position).WithZ( 0 ).Normal;
		var speedAfterVault = Entity.Velocity.WithZ( 0 ).Length;

		Entity.Velocity = vaultDirection * speedAfterVault;
		vaultSpeed = Math.Max(Entity.Velocity.Length, 200f);
	}

	[ConCmd.Admin( "noclip" )]
	static void DoPlayerNoclip()
	{
		if ( ConsoleSystem.Caller.Pawn is Pawn player )
		{
			if (player.Controller.Noclipping)
				player.Controller.Noclipping = false;
			else
			{
				player.Controller.Noclipping = true;
			}
		}
	}

	[ConCmd.Admin( "unlimited_sprint" )]
	static void DoUnlimitedSprint()
	{
		if ( ConsoleSystem.Caller.Pawn is Pawn player )
		{
			if ( player.Controller.UnlimitedSprint )
				player.Controller.UnlimitedSprint = false;
			else
				player.Controller.UnlimitedSprint = true;
		}
	}

	void HandleNoclipping()
	{
		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles;
		var moveVector = Rotation.From( angles ) * movement * 10f;

		Entity.Transform = Entity.Transform.Add( moveVector, true );
		Entity.Velocity = 0;
	}

	bool CheckForWallLeft()
	{
		var trace = Trace.Ray( Entity.Position + Vector3.Up * 50f, Entity.Position + Vector3.Up * 50f + Entity.Rotation.Left * 30f + Entity.Rotation.Forward * 15f ).Run();

		return trace.Hit && trace.Entity.IsWorld;
	}

	bool CheckForWallRight()
	{
		var trace = Trace.Ray( Entity.Position + Vector3.Up * 50f, Entity.Position + Vector3.Up * 50f + Entity.Rotation.Right * 30f + Entity.Rotation.Forward * 15f ).Run();

		return trace.Hit && trace.Entity.IsWorld;
	}

	void FootstepWizard()
	{
		float speed = Entity.Velocity.Length;

		if ( Game.IsServer )
			return;

		if ( speed == 0f )
			return;

		if ( !Grounded && !IsWallRunning() )
			return;

		TimeSinceLastFootstep += Time.Delta;
		TimeSinceLastFootstepRelease += Time.Delta;

		float nextStep = 80f / speed;
		String footstepSound = speed < 300 ? "concretefootstepwalk" : "concretefootsteprun";
		String footstepReleaseSound = IsWallRunning() ? "concretefootstepwallrunrelease" : "concretefootsteprunrelease";

		if ( IsWallRunning() )
		{
			nextStep = 75f / speed;
			footstepSound = "concretefootstepwallrun";
		}

		if (TimeSinceLastFootstep > nextStep )
		{
			Sound.FromWorld( footstepSound, Entity.Position + Vector3.Down * 10f );

			TimeSinceLastFootstep = 0f;
		}

		if ( TimeSinceLastFootstepRelease > nextStep*1.05 && speed > 400)
		{
			Sound.FromWorld( footstepReleaseSound, Entity.Position + Vector3.Down * 10f );

			TimeSinceLastFootstepRelease = 0f;
		}
	}

	bool IsWallRunning()
	{
		return Wallrunning != 0;
	}

	void CheckForSharpTurn(Vector3 moveVector)
	{
		if ( Entity.Velocity.Angle( moveVector.Normal * moveVector.Length ) > SharpTurnAngle )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, SpeedShrinkRate );
		}
	}

	void DoJump()
	{
		if ( CanJump() )
		{
			Entity.Velocity = ApplyJump( Entity.Velocity, "jump" );
		}
	}

	bool CanJump()
	{
		return Grounded && Vaulting == 0;
	}

	Entity CheckForGround()
	{
		if ( Entity.Velocity.z > 100f )
			return null;

		var trace = Entity.TraceBBox( Entity.Position, Entity.Position + Vector3.Down, 2f );

		if ( !trace.Hit )
			return null;

		if ( trace.Normal.Angle( Vector3.Up ) > GroundAngle )
			return null;

		return trace.Entity;
	}

	Vector3 ApplyFriction( Vector3 input, float frictionAmount )
	{
		float StopSpeed = 100.0f;

		var speed = input.Length;
		if ( speed < 0.1f ) return input;

		// Bleed off some speed, but if we have less than the bleed
		// threshold, bleed the threshold amount.
		float control = (speed < StopSpeed) ? StopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * Time.Delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;
		if ( newspeed == speed ) return input;

		newspeed /= speed;
		input *= newspeed;

		return input;
	}

	Vector3 Accelerate( Vector3 input, Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
	{
		if ( speedLimit > 0 && wishspeed > speedLimit )
			wishspeed = speedLimit;

		input = input.LerpTo( wishdir * wishspeed, acceleration );

		return input;
	}

	Vector3 ApplyJump( Vector3 input, string jumpType )
	{
		AddEvent( jumpType );

		return input + Vector3.Up * JumpSpeed;
	}

	Vector3 StayOnGround( Vector3 position )
	{
		var start = position + Vector3.Up * 2;
		var end = position + Vector3.Down * StepSize;

		// See how far up we can go without getting stuck
		var trace = Entity.TraceBBox( position, start );
		start = trace.EndPosition;

		// Now trace down from a known safe position
		trace = Entity.TraceBBox( start, end );

		if ( trace.Fraction <= 0 ) return position;
		if ( trace.Fraction >= 1 ) return position;
		if ( trace.StartedSolid ) return position;
		if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return position;

		return trace.EndPosition;
	}

	public bool HasEvent( string eventName )
	{
		return ControllerEvents.Contains( eventName );
	}

	void AddEvent( string eventName )
	{
		if ( HasEvent( eventName ) )
			return;

		ControllerEvents.Add( eventName );
	}
}
