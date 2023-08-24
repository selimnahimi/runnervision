using Sandbox;
using System;
using System.Collections.Generic;

namespace RunnerVision;

public partial class PawnController : EntityComponent<Pawn>
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
	public Vector3 ForwardDirection { get; set; }

	public float CurrentMaxSpeed { get; set; }
	private float TimeSinceLastFootstep { get; set; }
	private float TimeSinceLastFootstepRelease { get; set; }
	private Vector3 VaultTargetPos { get; set; }
	private Vector3 VaultStartPos { get; set; }

	private float bezierCounter = 0f;
	private float vaultSpeed = 0f;
	private bool debugMode => false;
	private bool parkouredSinceJumping = false;
	private bool wallrunSinceJumping = false;
	private int previousWallrunSide = 0;

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
			// TODO: don't return here
			HandleNoclipping();
			return;
		}

		if ( Vaulting != 0 )
		{
			// TODO: don't return here
			UpdateVault();
			return;
		}

		TestAndFixStuck();
		UpdateWallrunning();

		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles.WithPitch( 0 );
		var moveVector = Rotation.From( angles ) * movement * CurrentMaxSpeed;
		var groundEntity = CheckForGround();

		if ( moveVector.LengthSquared != 0 )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( MaxSpeed, Time.Delta * 50f * SpeedGrowthRate );
		}
		else
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, Time.Delta * 50f * SpeedShrinkRate );
		}

		AdjustSharpTurn( moveVector );

		if ( groundEntity.IsValid() )
		{
			if ( !Grounded )
			{
				// Landed on floor
				Sound.FromWorld( "concretefootstepland", Entity.Position + Vector3.Down * 10f );

				Entity.Velocity = Entity.Velocity.WithZ( 0 );
				AddEvent( "grounded" );
				Wallrunning = 0;
				previousWallrunSide = 0;

				parkouredSinceJumping = false;
			}

			Entity.Velocity = Accelerate( Entity.Velocity, moveVector.Normal, moveVector.Length, CurrentMaxSpeed, Acceleration );
			Entity.Velocity = ApplyFriction( Entity.Velocity, Friction );
		}
		else
		{
			Entity.Velocity += Vector3.Down * (IsWallRunning() ? Gravity * 0.75f : Gravity ) * Time.Delta;
		}

		if ( Input.Released( "jump" ) )
		{
			parkouredSinceJumping = false;
			wallrunSinceJumping = false;
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
		}

		// TODO: extract to function
		ForwardDirection = Entity.Velocity.EulerAngles.ToRotation().Forward.WithZ( 0 );
		var cameraDirection = Camera.Rotation.Forward.WithZ( 0 );
		var forwardAngle = ForwardDirection.Angle( cameraDirection );

		// Check angle from movement axis (max 90 degrees)
		float dotProduct = Vector3.Dot( ForwardDirection, cameraDirection );
		if ( dotProduct < 0 )
		{
			forwardAngle = 180 - forwardAngle;
		}

		if ( Input.Pressed( "jump" ))
		{
			if ( IsWallRunning() )
			{

				var forwardMultiplier = Math.Max(0.2f, forwardAngle / 90f);

				var jumpVector = cameraDirection * 300f * forwardMultiplier + Entity.Rotation.Up * 250f;

				Entity.Velocity *= 0.5f;

				Entity.ApplyAbsoluteImpulse( jumpVector );

				previousWallrunSide = Wallrunning;
				Wallrunning = 0;
			}
			
			if ( !IsWallRunning() && !IsDashing() )
			{
				DoJump();
			}
		}

		if ( Input.Down( "jump" ) && !parkouredSinceJumping )
		{
			bool successfulWallrun = TryWallrunning( 1 ) || TryWallrunning( 2 );

			if ( !successfulWallrun )
				TryVaulting();
		}

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
		IncreaseDeltaTime();
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
