using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	void ClampMaxSpeed()
	{
		CurrentMaxSpeed = Math.Min( CurrentMaxSpeed, MaxSpeed );
	}

	bool IsDashing()
	{
		return Dashing != 0;
	}

	void UnStuck()
	{
		// TODO: make this smarter
		Entity.Position += Entity.Rotation.Up;
	}

	bool IsStuck()
	{
		var result = Entity.TraceBBox( Entity.Position, Entity.Position );
		return result.Hit;
	}

	void TestAndFixStuck()
	{
		if ( IsStuck() )
		{
			UnStuck();
		}
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
	}

	void FootstepWizard()
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

	void DoJump()
	{
		if ( CanJump() )
		{
			Entity.Velocity = ApplyJump( Entity.Velocity, "jump" );
		}
	}

	bool CanJump()
	{
		return Grounded && !IsVaulting();
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

		input = input.LerpTo( wishdir * wishspeed, Time.Delta * 45f * acceleration );

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
}
