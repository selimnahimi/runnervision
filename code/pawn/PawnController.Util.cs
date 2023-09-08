using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	Rotation GetVelocityRotation()
	{
		return Entity.Velocity.EulerAngles.ToRotation();
	}

	bool AngleWithinRange(Vector3 directionVector1, Vector3 directionVector2, float minAngle = 0f, float maxAngle = 360f)
	{
		if ( directionVector1.Normal.Angle( directionVector2 ) > maxAngle )
			return false;

		if ( directionVector1.Normal.Angle( directionVector2 ) < minAngle )
			return false;

		return true;
	}

	Vector3 GetMoveVector()
	{
		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles.WithPitch( 0 );
		return Rotation.From( angles ) * movement * CurrentMaxSpeed;
	}

	void UpdateDash()
	{
		if ( TimeSinceDash > 0.5f )
			Dashing = 0;
	}

	void InitiateJumpOffWall()
	{
		var cameraDirection = GetCameraDirection();
		var forwardAngle = GetForwardAngle();

		var forwardMultiplier = Math.Max( 0.5f, forwardAngle / 90f );

		var jumpVector = cameraDirection * 300f * forwardMultiplier + Entity.Rotation.Up * 300f;

		Entity.Velocity *= 0.5f;

		Entity.ApplyAbsoluteImpulse( jumpVector );

		previousWallrunNormal = CurrentWall.Normal;
		Wallrunning = 0;
	}

	Vector3 GetCameraDirection()
	{
		return Camera.Rotation.Forward.WithZ( 0 );
	}

	float GetForwardAngle()
	{
		var cameraDirection = GetCameraDirection();
		var forwardAngle = ForwardDirection.Angle( cameraDirection );

		// Check angle from movement axis (max 90 degrees)
		float dotProduct = Vector3.Dot( ForwardDirection, cameraDirection );

		if ( dotProduct < 0 )
		{
			return 180 - forwardAngle;
		}

		return forwardAngle;
	}

	void InitiateDash()
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

	void DisableParkourLock()
	{
		parkouredSinceJumping = false;
		wallrunSinceJumping = false;
	}

	void DoFall()
	{
		Entity.Velocity += Vector3.Down * (IsWallRunning() ? Gravity * 0.60f : Gravity) * Time.Delta;
	}

	void InitiateLandingOnFloor()
	{
		Sound.FromWorld( "concretefootstepland", Entity.Position + Vector3.Down * 10f );
		AddEvent( "grounded" );

		Wallrunning = 0;
		previousWallrunNormal = Vector3.Zero;

		parkouredSinceJumping = false;
		parkouredBeforeLanding = false;

		Jumping = false;

		if ( Entity.Velocity.Length > 100f )
		{
			CurrentMaxSpeed += 500;
		}
	}

	void UpdateMaxSpeed(Vector3 moveVector)
	{
		if ( moveVector.LengthSquared != 0 )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( MaxSpeed, Time.Delta * 50f * SpeedGrowthRate );
		}
		else
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, Time.Delta * 50f * SpeedShrinkRate );
		}
	}

	void ClampMaxSpeed()
	{
		CurrentMaxSpeed = Math.Min( CurrentMaxSpeed, MaxSpeed );
	}

	bool IsDashing()
	{
		return Dashing != 0;
	}

	bool ShouldDash()
	{
		return Grounded && !Input.Down( "forward" ) && (Input.Down( "left" ) || Input.Down( "right" ));
	}

	void HandleNoclipping()
	{
		var movement = Entity.InputDirection.Normal;
		var angles = Entity.ViewAngles;
		var moveVector = Rotation.From( angles ) * movement * 10f;

		Entity.Transform = Entity.Transform.Add( moveVector, true );
		Entity.Velocity = 0;
	}

	void IncreaseDeltaTime()
	{
		TimeSinceLastFootstep += Time.Delta;
		TimeSinceLastFootstepRelease += Time.Delta;
		TimeSinceDash += Time.Delta;
		TimeSinceClimbing += Time.Delta;
		TimeSinceWallrun += Time.Delta;
	}

	void UpdateFootsteps()
	{
		float speed = Entity.Velocity.Length;

		if ( Game.IsServer )
			return;

		if ( speed == 0f )
			return;

		if ( !Grounded && !IsWallRunning() && !Climbing )
			return;

		float nextStep = 70f / speed;
		String footstepSound = speed < 300 ? "concretefootstepwalk" : "concretefootsteprun";
		String footstepReleaseSound = IsWallRunning() ? "concretefootstepwallrunrelease" : "concretefootsteprunrelease";

		if ( IsWallRunning() )
		{
			nextStep = 60f / speed;
			footstepSound = "concretefootstepwallrun";
		}

		if ( Climbing )
		{
			nextStep = 0.2f;
			footstepSound = "concretefootstepwallrun";
		}

		if ( TimeSinceLastFootstep > nextStep )
		{
			Sound.FromWorld( footstepSound, Entity.Position + Vector3.Down * 10f );

			TimeSinceLastFootstep = 0f;
		}

		if ( TimeSinceLastFootstepRelease > nextStep * 1.15 && speed > StartFootSoundVelocity )
		{
			Sound.FromWorld( footstepReleaseSound, Entity.Position + Vector3.Down * 10f );

			TimeSinceLastFootstepRelease = 0f;
		}
	}

	void AdjustSharpTurn( Vector3 moveVector )
	{
		if ( Entity.Velocity.Angle( moveVector.Normal * moveVector.Length ) > SharpTurnAngle )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, Time.Delta * 50f * SpeedShrinkRate );
		}
	}

	void InitiateJump()
	{
		if ( CanJump() )
		{
			Jumping = true;
			Entity.Velocity = ApplyJump( Entity.Velocity, "jump" );
		}
	}

	bool CanJump()
	{
		if ( !Grounded )
			return false;

		if ( IsVaulting() )
			return false;

		if ( IsDashing() )
			return false;

		if ( IsWallRunning() )
			return false;

		return true;
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

	void DoMovement( Vector3 moveVector )
	{
		Entity.Velocity = Accelerate( Entity.Velocity, moveVector.Normal, moveVector.Length, CurrentMaxSpeed, Acceleration );
		Entity.Velocity = ApplyFriction( Entity.Velocity, Friction );
	}

	Vector3 Accelerate( Vector3 input, Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
	{
		if ( speedLimit > 0 && wishspeed > speedLimit )
			wishspeed = speedLimit;

		input = input.LerpTo( wishdir * wishspeed, Time.Delta * 45f * acceleration );

		return input;
	}

	Vector3 ApplyJump( Vector3 input, string jumpType )
	{
		AddEvent( jumpType );

		return input + Vector3.Up * JumpSpeed;
	}

	void UpdateMoveHelper( Entity groundEntity )
	{
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
}
