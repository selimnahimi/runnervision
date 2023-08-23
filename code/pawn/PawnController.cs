using Sandbox;
using Sandbox.Internal;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Numerics;

namespace MyGame;

public class PawnController : EntityComponent<Pawn>
{
	public int StepSize => 26;
	public int GroundAngle => 100;
	public int JumpSpeed => 300;
	public float Gravity => 800f;
	public float StartingSpeed => 1000f;
	public float MaxSpeed => 1500f;
	public float SpeedGrowthRate => 2.0f;
	public float SpeedShrinkRate => 50.0f;
	public float Friction => 3.0f;
	public float SharpTurnAngle => 50f;
	public float Acceleration => 0.02f;
	public float StartFootSoundVelocity => 300f;
	public int Wallrunning { get; set; }
	public int Dashing { get; set; }
	public bool Noclipping { get; set; }
	public bool UnlimitedSprint { get; set; }
	public int Vaulting { get; set; }
	public float TimeSinceDash { get; set; }

	private float CurrentMaxSpeed { get; set; }
	private float TimeSinceLastFootstep { get; set; }
	private float TimeSinceLastFootstepRelease { get; set; }
	private Vector3 VaultTargetPos { get; set; }
	private Vector3 VaultStartPos { get; set; }

	private float bezierCounter = 0f;
	private float vaultSpeed = 0f;
	private bool debugMode = true; /*RunnerVision.CurrentRunnerVision().DebugMode*/
	private bool parkouredSinceJumping = false;

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

		if ( Wallrunning != 0 )
			UpdateWallrunning();

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
				// Landed on floor
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

		if ( Grounded && parkouredSinceJumping && !Input.Down( "jump" ) )
		{
			parkouredSinceJumping = false;
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( ShouldDash() )
			{
				if ( TimeSinceDash > 1.0f )
				{
					var isLeft = Input.Down( "left" );

					Dashing = isLeft ? 1 : 2;

					Entity.ApplyAbsoluteImpulse( (isLeft ? Entity.Rotation.Left : Entity.Rotation.Right) * 300f );
					CurrentMaxSpeed += 200f;

					TimeSinceDash = 0.0f;
				}
			} 
			else
			{
				if ( IsWallRunning() )
				{
					Entity.Velocity *= 0.5f;
					
					Entity.ApplyAbsoluteImpulse( Camera.Rotation.Forward * 250f + Entity.Rotation.Up * 100f );
					Wallrunning = 0;

					parkouredSinceJumping = false;
				}
				else
				{
					DoJump();
				}
			}
		}

		if ( Input.Down( "jump" ) && !Grounded && !parkouredSinceJumping )
		{
			if ( CanWallrun() && CheckForWall( isWallrunningOnRightSide: false ) )
			{
				Wallrunning = 1;
			}
			else if ( CanWallrun() && CheckForWall( isWallrunningOnRightSide: true ) )
			{
				Wallrunning = 2;
			}
			else
			{
				TryVaulting();
			}
		}

		TimeSinceDash += Time.Delta;

		if ( TimeSinceDash > 0.5f )
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

		if (debugMode)
		{
			DebugOverlay.ScreenText( CurrentMaxSpeed.ToString() );
			DebugOverlay.ScreenText( Entity.Position.ToString(), 3 );

			DebugOverlay.ScreenText( Vaulting.ToString(), 1 );
		}

		FootstepWizard();
	}

	bool ShouldDash()
	{
		return Grounded && !Input.Down( "forward" ) && (Input.Down( "left" ) || Input.Down( "right" ));
	}

	void UpdateWallrunning()
	{
		if ( !CanWallrun() )
			Wallrunning = 0;
	}

	bool CanWallrun()
	{
		if ( Wallrunning == 1 && !CheckForWall( isWallrunningOnRightSide: false, behind: false ) )
			return false;

		if ( Wallrunning == 2 && !CheckForWall( isWallrunningOnRightSide: true, behind: false ) )
			return false;

		if ( Entity.Velocity.WithZ( 0 ).Length < 150f )
			return false;

		return true;
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
		// TODO: make smoother

		Vector3 pos = BezierApproach(
			start: VaultStartPos,
			end: VaultTargetPos,
			curveSize: 30f,
			t: bezierCounter
		);

		bezierCounter += (vaultSpeed/100) * Time.Delta;

		Entity.Position = Entity.Position.LerpTo( pos, 0.5f );

		if ( bezierCounter >= 1.0f )
			Vaulting = 0;
	}

	void TryVaulting()
	{
		if ( Grounded && !Input.Down( "forward" ) )
			return;

		float speed = Entity.Velocity.Length;

		if ( speed.AlmostEqual(0f) )
			return;

		float rayDistance = Math.Max( (speed / 500) * 60f, 40f );

		float showDebugTime = 3f;
		float boxRadius = 20f;
		var distanceBehindObstacle = rayDistance * 1.20f + 60f;

		BBox boxFront = new BBox(center: 0, size: 40f)
			.Translate( Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 30f );

		var traceFront = Trace.Box(
			bbox: boxFront,
			from: 0, to: 0
		).Run();

		if ( debugMode )
			DebugOverlay.Box(
				bounds: new BBox( Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 20f, 30f ),
				color: Color.Red,
				duration: showDebugTime
			);

		if ( !traceFront.Hit  )
			return;

		BBox boxBehindObstacle = new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 70f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Up * 10f + Vector3.Right * boxRadius
		).Translate( Entity.Position + Entity.Rotation.Forward * distanceBehindObstacle );

		if ( debugMode )
			DebugOverlay.Box( bounds: boxBehindObstacle, color: Color.Blue, duration: showDebugTime );

		var traceBehindObstacle = Trace.Box(
			bbox: boxBehindObstacle,
			from: 0, to: 0
		).Run();

		// Make sure we are not vaulting inside map geometry
		var traceWallFailsafe = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 60f,
			to: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle
		).Run();

		var hitFailsafe = traceWallFailsafe.Entity?.IsValid == true; //bool? needs to be converted to bool

		if ( debugMode )
			DebugOverlay.Line(
				start: Entity.Position + Entity.Rotation.Up * 60f,
				end: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle,
				duration: showDebugTime
			);

		var fallAfterVault = false;

		if (!traceBehindObstacle.Hit && !hitFailsafe )
		{
			// Vault over

			// Check if there's space to vault over
			var topBoxSmall = new BBox(
				mins: Vector3.Forward * +boxRadius * 0.70f + Vector3.Up * 50f + Vector3.Left * boxRadius * 0.70f,
				maxs: Vector3.Forward * -boxRadius * 0.70f + Vector3.Up * 80f + Vector3.Right * boxRadius * 0.70f
			).Translate( Entity.Position + Entity.Rotation.Forward * rayDistance );

			var traceBoxSmallAboveObstacle = Trace.Box(
				bbox: topBoxSmall,
				from: 0, to: 0
			).Run();

			if ( debugMode )
				DebugOverlay.Box( bounds: topBoxSmall, color: Color.Green, duration: showDebugTime );

			if ( traceBoxSmallAboveObstacle.Hit )
				return;

			// Cast a ray to check where the ground is
			var traceObstacleSurface = Trace.Ray(
				from: boxBehindObstacle.Center + Entity.Rotation.Up * 60f,
				to: boxBehindObstacle.Center + Entity.Rotation.Up * -60f
			).Run();

			if ( debugMode )
				DebugOverlay.Line(
					start: boxBehindObstacle.Center + Entity.Rotation.Up * 60f,
					end: boxBehindObstacle.Center + Entity.Rotation.Up * -60f,
					color: Color.Blue, duration: showDebugTime
				);

			if ( traceObstacleSurface.Hit )
			{
				var groundPosition = traceObstacleSurface.HitPosition;

				VaultTargetPos = groundPosition + Vector3.Up * 10f;
			}
			else
			{
				VaultTargetPos = boxBehindObstacle.Center + Entity.Rotation.Up * -40f;
				fallAfterVault = true;
			}

			Vaulting = 2;
			vaultSpeed = 200f;
		}
		else
		{
			// Vault onto

			// Make sure there's enough space to stand on obstacle
			var topBoxLarge = new BBox(
				mins: Vector3.Forward * +boxRadius + Vector3.Up * 45f + Vector3.Left * boxRadius,
				maxs: Vector3.Forward * -boxRadius + Vector3.Up * 115f + Vector3.Right * boxRadius
			).Translate( Entity.Position + Entity.Rotation.Forward * rayDistance );

			var traceBoxLargeAboveObstacle = Trace.Box(
				bbox: topBoxLarge,
				from: 0, to: 0
			).Run();

			if ( debugMode )
				DebugOverlay.Box( bounds: topBoxLarge, color: Color.Green, duration: showDebugTime );

			if ( traceBoxLargeAboveObstacle.Hit )
				return;

			// Cast a ray to check where the ground is
			var traceObstacleSurface = Trace.Ray(
				from: topBoxLarge.Center + Entity.Rotation.Up * 60f,
				to: topBoxLarge.Center + Entity.Rotation.Up * -60f
			).Run();

			if ( debugMode )
				DebugOverlay.Line(
					start: topBoxLarge.Center + Entity.Rotation.Up * 60f,
					end: topBoxLarge.Center + Entity.Rotation.Up * -60f,
					color: Color.Blue, duration: showDebugTime
				);

			if ( !traceObstacleSurface.Hit )
				return;

			var groundPosition = traceObstacleSurface.HitPosition;

			VaultTargetPos = groundPosition + Vector3.Up * 13f;
			Vaulting = 1;
			vaultSpeed = Math.Max( Entity.Velocity.Length, 200f );
		}

		parkouredSinceJumping = true;

		VaultStartPos = Entity.Position;
		bezierCounter = 0f;

		Vector3 vaultDirection;

		if ( fallAfterVault )
			vaultDirection = Entity.Rotation.Forward * 0.4f + Entity.Rotation.Down * 0.3f;
		else
			vaultDirection = (VaultTargetPos - Entity.Position).WithZ( 0 ).Normal;

		var speedAfterVault = Entity.Velocity.WithZ( 0 ).Length;

		Entity.Velocity = vaultDirection * speedAfterVault;
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

	bool CheckForWall( bool isWallrunningOnRightSide, bool behind = false )
	{
		var leftDirection = Entity.Velocity.EulerAngles.ToRotation().Left;
		var forwardDirection = Entity.Velocity.EulerAngles.ToRotation().Forward;

		var from = Entity.Position + Vector3.Up * 50f;
		var to = Entity.Position + Vector3.Up * 50f + leftDirection * (isWallrunningOnRightSide ? -30f : 30f) + forwardDirection * (behind ? -15f : 15f);


		if ( debugMode )
			DebugOverlay.Line( start: from, end: to, duration: 1f );

		var trace = Trace.Ray(from, to).Run();

		return trace.Hit;
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

		if ( TimeSinceLastFootstepRelease > nextStep*1.15 && speed > StartFootSoundVelocity )
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
