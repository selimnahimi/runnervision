﻿using Sandbox;
using System;
using System.Collections.Generic;

namespace MyGame;

public class PawnController : EntityComponent<Pawn>
{
	public int StepSize => 24;
	public int GroundAngle => 45;
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

	private float CurrentMaxSpeed { get; set; }
	private float TimeSinceLastFootstep { get; set; }
	private float TimeSinceLastFootstepRelease { get; set; }
	private float TimeSinceDash { get; set; }
	// private float NextFootstep { get; set; }

	private float CurrentAcceleration { get; set; }

	HashSet<string> ControllerEvents = new( StringComparer.OrdinalIgnoreCase );

	bool Grounded => Entity.GroundEntity.IsValid();

	public PawnController()
	{
		CurrentMaxSpeed = StartingSpeed;
	}

	public void Simulate( IClient cl )
	{
		ControllerEvents.Clear();

		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles.WithPitch( 0 );
		var moveVector = Rotation.From( angles ) * movement * CurrentMaxSpeed;
		var groundEntity = CheckForGround();

		// Log.Info( moveVector );
		if ( moveVector.LengthSquared != 0 )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( MaxSpeed, SpeedGrowthRate );
		}
		else
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, SpeedShrinkRate );
		}

		CheckForSharpTurn( moveVector );

		//Log.Info( CurrentMaxSpeed );
		DebugOverlay.ScreenText( CurrentMaxSpeed.ToString() );

		if ( groundEntity.IsValid() )
		{
			if ( !Grounded )
			{
				Sound.FromWorld( "concretefootstepland", Entity.Position + Vector3.Down * 10f );

				Entity.Velocity = Entity.Velocity.WithZ( 0 );
				AddEvent( "grounded" );
			}

			Entity.Velocity = Accelerate( Entity.Velocity, moveVector.Normal, moveVector.Length, CurrentMaxSpeed, Acceleration );
			Entity.Velocity = ApplyFriction( Entity.Velocity, Friction );

			//Log.Info( Entity.Velocity.Distance( moveVector.Normal * moveVector.Length ) );
			// Log.Info( Entity.Velocity.Angle( moveVector.Normal * moveVector.Length ) );

			/*if ( Entity.Velocity.Angle( moveVector.Normal * moveVector.Length ) > 30 )
			{
				Entity.Velocity = ApplyFriction( Entity.Velocity, 4.0f );
			}*/
			//if ( Entity.Velocity.Distance(moveVector.Normal) )
			//Entity.Velocity = ApplyFriction( Entity.Velocity, 4.0f );
		}
		else
		{
			// Entity.Velocity = Accelerate( Entity.Velocity, moveVector.Normal, moveVector.Length, CurrentMaxSpeed/2, Acceleration );
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
				DoJump();
			}

			if ( IsWallRunning() )
			{
				Entity.ApplyAbsoluteImpulse( Entity.Rotation.Forward * 250f + Vector3.Up * 50f );
				Wallrunning = 0;
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

		FootstepWizard();
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
		if ( Grounded )
		{
			Entity.Velocity = ApplyJump( Entity.Velocity, "jump" );
		}
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

		/*var currentspeed = input.Dot( wishdir );
		var addspeed = wishspeed - currentspeed;

		if ( addspeed <= 0 )
			return input;

		bool goingFast = currentspeed > 0.2 * wishspeed;

		var accelspeed = acceleration * Time.Delta * wishspeed;
		Log.Info( accelspeed );

		if ( accelspeed > addspeed )
			accelspeed = addspeed;*/

		var currentspeed = input.Dot( wishdir );

		// Log.Info( wishspeed + " / " + currentspeed );
		// Log.Info( acceleration );

		input = input.LerpTo( wishdir * wishspeed, acceleration );

		if (wishdir.Length < 0.1f )
		{
			// input = input.LerpTo( wishdir, 0.005f );
		}

		// Log.Info( Entity.Velocity.Length );

		// input += wishdir * accelspeed;

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
