using Sandbox;
using System;
using System.Diagnostics.Tracing;
using System.Reflection.Metadata.Ecma335;

namespace RunnerVision;

public partial class PawnController
{
	public enum WallRunSide
	{
		None,
		Left,
		Right
	}

	public struct WallRunTrace
	{
		public WallRunSide side;
		public TraceResult traceResult;

		public WallRunTrace( WallRunSide side, TraceResult traceResult )
		{
			this.side = side;
			this.traceResult = traceResult;
		}

		public static WallRunTrace None =>
			new WallRunTrace( WallRunSide.None, new TraceResult() );
	}

	void UpdateWallrunning()
	{
		if ( Wallrunning == 0 )
			return;

		var traceWall = CheckForWall();

		if ( !CanWallrun( traceWall ) )
		{
			Wallrunning = 0;
			wallrunSinceJumping = false;
			previousWallrunSide = 0;
		}

		CurrentWall = traceWall.traceResult;
	}

	public bool IsWallRunning()
	{
		return Wallrunning != 0;
	}

	bool CanWallrun( WallRunTrace traceWall )
	{
		if ( traceWall.side == WallRunSide.None )
			return false;

		if ( IsDashing() )
			return false;

		if ( Grounded )
			return false;

		if ( Wallrunning == WallRunSide.Left && traceWall.side != WallRunSide.Left )
			return false;

		if ( Wallrunning == WallRunSide.Right && traceWall.side != WallRunSide.Right )
			return false;

		if ( !IsWallRunning() && (Entity.Velocity * 0.5f).WithZ( 0 ).Length < 100f )
			return false;

		if ( IsWallRunning() && Entity.Velocity.WithZ( 0 ).Length < 100f )
			return false;

		if ( !AngleWithinRange( GetVelocityRotation().Forward, traceWall.traceResult.Normal, maxAngle: 110f ) )
			return false;

		return true;
	}

	bool TryWallrunning( )
	{
		if ( wallrunSinceJumping )
			return false;

		var traceWall = CheckForWall();

		if ( CanWallrun( traceWall ) && previousWallrunSide != traceWall.side )
		{
			InitiateWallrun( traceWall );

			return true;
		}

		return false;
	}

	void InitiateWallrun( WallRunTrace traceWall )
	{
		if ( !IsWallRunning() && !Grounded )
		{
			var velocityZ = Math.Max( 100f, Entity.Velocity.z );

			Entity.Velocity *= 0.5f;
			Entity.ApplyAbsoluteImpulse( ForwardDirection * 100f );
			Entity.Velocity = Entity.Velocity.WithZ( velocityZ );
		}

		Wallrunning = traceWall.side;
		previousWallrunSide = traceWall.side;

		wallrunSinceJumping = true;
		parkouredBeforeLanding = true;

		TimeSinceWallrun = 0f;
	}

	WallRunTrace CheckForWall( bool behind = false )
	{
		var velocityRotation = GetVelocityRotation();

		var from = Entity.Position + Vector3.Up * 50f;
		var toLeft = Entity.Position + Vector3.Up * 50f + velocityRotation.Left * 30f + velocityRotation.Forward * (behind ? -15f : 15f);
		var toRight = Entity.Position + Vector3.Up * 50f + velocityRotation.Right * 30f + velocityRotation.Forward * (behind ? -15f : 15f);

		if ( debugMode )
		{
			DebugOverlay.Line( start: from, end: toLeft, duration: 1f );
			DebugOverlay.Line( start: from, end: toRight, duration: 1f );
		}

		var traceLeft = Trace.Ray( from, toLeft ).Run();
		var traceRight = Trace.Ray( from, toRight ).Run();

		if ( traceLeft.Hit )
			return new WallRunTrace( WallRunSide.Left, traceLeft );

		if ( traceRight.Hit )
			return new WallRunTrace( WallRunSide.Right, traceRight );

		return WallRunTrace.None;
	}
}
