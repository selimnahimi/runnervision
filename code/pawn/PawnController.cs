using Sandbox;
using System;
using System.Collections.Generic;

namespace RunnerVision;

public partial class PawnController : EntityComponent<Pawn>
{
	public int StepSize => 26;
	public int GroundAngle => 200;
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
	public int MaxClimbAmount => 4;
	public bool Climbing { get; set; }
	public WallRunSide Wallrunning { get; set; }
	public int Dashing { get; set; }
	public bool Noclipping { get; set; }
	public bool UnlimitedSprint { get; set; }
	public VaultType Vaulting { get; set; }
	public float TimeSinceDash { get; set; }
	public Vector3 ForwardDirection { get; set; }
	public float TimeSinceClimbing { get; set; }
	public float TimeSinceWallrun { get; set; }
	public TraceResult CurrentWall { get; set; } = new TraceResult();

	private int CurrentClimbAmount { get; set; }
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
	private WallRunSide previousWallrunSide = WallRunSide.None;
	private bool parkouredBeforeLanding = false;

	HashSet<string> ControllerEvents = new( StringComparer.OrdinalIgnoreCase );

	public bool Grounded => Entity.GroundEntity.IsValid();

	public PawnController()
	{
		CurrentMaxSpeed = StartingSpeed;
	}

	public void Simulate( IClient cl )
	{
		ControllerEvents.Clear();

		DebugOverlay.ScreenText( "Climbing: " + IsClimbing().ToString(), line: 0 );
		DebugOverlay.ScreenText( "Wallrunning: " + Wallrunning.ToString(), line: 1 );
		DebugOverlay.ScreenText( "Vaulting: " + Vaulting.ToString(), line: 2 );
		DebugOverlay.ScreenText( "Current Speed: " + ((int)Entity.Velocity.Length).ToString(), line: 3 );
		DebugOverlay.ScreenText( "Current Accel: " + CurrentMaxSpeed.ToString(), line: 4 );
		DebugOverlay.ScreenText( "Max Accel: " + MaxSpeed.ToString(), line: 5 );

		if ( Noclipping )
		{
			// TODO: don't return here
			HandleNoclipping();
			return;
		}

		if ( IsVaulting() )
		{
			// TODO: don't return here
			ProgressVault();
			return;
		}

		UpdateWallrunning();

		var groundEntity = CheckForGround();
		var moveVector = GetMoveVector();

		UpdateMaxSpeed( moveVector );
		AdjustSharpTurn( moveVector );

		if ( groundEntity.IsValid() )
		{
			if ( !Grounded )
			{
				InitiateLandingOnFloor();
			}

		}

		UpdateMoveHelper( groundEntity );

		if (Grounded)
		{
			DoMovement( moveVector );
		}
		else
		{
			DoFall();
		}

		if ( Input.Released( "jump" ) )
		{
			DisableParkourLock();
		}

		if ( Input.Pressed( "jump" ) )
		{
			if ( ShouldDash() )
			{
				InitiateDash();
			}
		}


		if ( Input.Pressed( "jump" ))
		{
			if ( IsWallRunning() )
			{
				InitiateJumpOffWall();
			}
			
			if ( !IsWallRunning() && !IsDashing() )
			{
				InitiateJump();
			}
		}

		if ( Input.Down( "jump" ) && !parkouredSinceJumping )
		{
			bool successfulWallrun = TryWallrunning( );

			if ( !successfulWallrun )
				InitiateVault();
		}

		UpdateDash();

		// TestAndFixStuck( ); // This causes the slope glitch

		if ( UnlimitedSprint )
			CurrentMaxSpeed = MaxSpeed;

		if (debugMode)
		{
			DebugOverlay.ScreenText( CurrentMaxSpeed.ToString() );
			DebugOverlay.ScreenText( Entity.Position.ToString(), 3 );

			DebugOverlay.ScreenText( Vaulting.ToString(), 1 );
		}

		UpdateFootsteps();
		IncreaseDeltaTime();
		UpdateClimbing();

		ClampMaxSpeed();

		ForwardDirection = GetVelocityRotation().Forward.WithZ( 0 );
	}

	[ConCmd.Server( "noclip" )]
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
