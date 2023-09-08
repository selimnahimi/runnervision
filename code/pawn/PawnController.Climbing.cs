﻿using Sandbox;
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
			StopClimbing();
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

		if ( ShouldInitiateClimb( traceFront ) )
			InitiateClimbing( traceFront );

		if ( IsClimbing() )
		{
			DoClimbing();
		}
	}

	bool ShouldInitiateClimb( TraceResult traceFront )
	{
		return !IsClimbing() && CanClimb( traceFront );
	}

	bool CanClimb( TraceResult traceFront )
	{
		var cameraDirection = GetCameraDirection();

		// Don't climb when facing wall at a wide angle
		if ( !AngleWithinRange( cameraDirection, traceFront.Normal, minAngle: 150f ) )
			return false;

		BBox boxInfrontOfWall = GetBoxInfrontOfWall( traceFront );

		if ( debugMode )
			DebugOverlay.Box(
				bounds: boxInfrontOfWall,
				color: Color.Orange,
				duration: showDebugTime
			);

		TraceResult traceBoxInfrontOfWall = Trace.Box(
			bbox: boxInfrontOfWall,
			from: 0, to: 0
		).Run();

		if ( traceBoxInfrontOfWall.Hit )
			return false;

		return true;
	}

	BBox GetBoxInfrontOfWall( TraceResult traceFront )
	{
		return new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 45f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Up * 120f + Vector3.Right * boxRadius
		).Translate( traceFront.HitPosition + Vector3.Down * 80f + traceFront.Normal * (boxRadius + 10f) );
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
		CurrentWall = traceFront;

		Camera.Rotation = new Rotation(traceFront.Normal, 10f);
		climbTargetXY = traceFront.HitPosition + Entity.Rotation.Down * 50f + traceFront.Normal * 30f;
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
		Entity.Velocity = Entity.Velocity.LerpTo(Entity.Velocity.WithX( 0 ).WithY( 0 ), 10f * Time.Delta);

		if ( TimeSinceClimbing > 0.15f )
		{
			var addHorizontalSpeed = Math.Min(50f, Entity.Velocity.WithZ( 0 ).Length);
			Entity.ApplyAbsoluteImpulse( Entity.Rotation.Up * 100f + Entity.Rotation.Up * addHorizontalSpeed );
			TimeSinceClimbing = 0f;
			CurrentClimbAmount++;
		}

		ApproachClimbTarget();
	}

	bool IsClimbing()
	{
		return Climbing;
	}
}
