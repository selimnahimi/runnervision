using Sandbox;
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
	private float CurrentMaxSpeed { get; set; }

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

		Log.Info( moveVector );
		if ( moveVector.LengthSquared != 0 )
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( MaxSpeed, SpeedGrowthRate );
		}
		else
		{
			CurrentMaxSpeed = CurrentMaxSpeed.Approach( StartingSpeed, SpeedShrinkRate );
		}

		CheckForSharpTurn( moveVector );

		Log.Info( CurrentMaxSpeed );

		if ( groundEntity.IsValid() )
		{
			if ( !Grounded )
			{
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
			Entity.Velocity += Vector3.Down * Gravity * Time.Delta;
		}

		if ( Input.Pressed( "jump" ) )
		{
			DoJump();
		}

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

		Log.Info( CheckForWall() );
		Log.Info( moveVector.Normal );
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

	bool CheckForWall()
	{
		var leftTrace = Entity.TraceBBox( Entity.Position, Entity.Position + Entity.Rotation.Right * 4f, 2f );
		var rightTrace = Entity.TraceBBox( Entity.Position, Entity.Position + Entity.Rotation.Left * 4f, 2f );

		if ( leftTrace.Hit && leftTrace.Entity.IsWorld )
			return true;

		if ( rightTrace.Hit && rightTrace.Entity.IsWorld )
			return true;

		return false;
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

		Log.Info( Entity.Velocity.Length );

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
