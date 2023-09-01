using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	private Vector3 climbTargetXY = Vector3.Zero;

	void UpdateClimbing()
	{
		if ( IsWallRunning() || IsVaulting() || Grounded )
		{
			TimeSinceClimbing = 0f;
			CurrentClimbAmount = 0;
			return;
		}

		var traceFront = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 15f,
			to: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 35f
		).Run();

		if ( debugMode )
			DebugOverlay.Line(
				start: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 15f,
				end: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 35f
			);

		if ( !ShouldClimb( traceFront ) )
		{
			StopClimbing();
			return;
		}

		if ( !IsClimbing() )
			InitiateClimbing( traceFront );

		DoClimbing();
		ApproachClimbTarget();
	}

	bool ShouldClimb( TraceResult traceFront )
	{
		if ( !Input.Down( "jump" ) )
			return false;

		if ( Entity.Velocity.z < 0 )
			return false;

		if ( CurrentClimbAmount >= MaxClimbAmount )
			return false;

		if ( !traceFront.Hit )
			return false;

		return true;
	}

	void InitiateClimbing(TraceResult traceFront)
	{
		Climbing = true;

		Camera.Rotation = new Rotation(traceFront.Normal, 10f);
		climbTargetXY = traceFront.HitPosition + Entity.Rotation.Down * 50f + traceFront.Normal * 20f;
	}

	void ApproachClimbTarget()
	{
		Vector3 newPos = Entity.Position.WithZ(0).LerpTo( climbTargetXY, Time.Delta * 10f );

		Entity.Position = new Vector3(newPos.x, newPos.y, Entity.Position.z);
	}

	void StopClimbing()
	{
		Climbing = false;
	}

	void DoClimbing()
	{
		Entity.Velocity = Entity.Velocity.WithX( 0 ).WithY( 0 );

		if ( TimeSinceClimbing > 0.15f )
		{
			Entity.ApplyAbsoluteImpulse( Entity.Rotation.Up * 100f );
			TimeSinceClimbing = 0f;
			CurrentClimbAmount++;
		}
	}

	bool IsClimbing()
	{
		return Climbing;
	}
}
