using Sandbox;

namespace RunnerVision;

public partial class PawnController
{
	void UpdateWallrunning()
	{
		if ( Wallrunning == 0 )
			return;

		if ( !CanWallrun() )
			Wallrunning = 0;
	}

	bool IsWallRunning()
	{
		return Wallrunning != 0;
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

	bool TryWallrunning( int side )
	{
		if ( wallrunSinceJumping )
			return false;

		// TODO: replace side with enum
		bool isWallrunningOnRightSide = side == 2 ? true : false;

		if ( CanWallrun() && previousWallrunSide != side && CheckForWall( isWallrunningOnRightSide ) )
		{
			if ( !IsWallRunning() && !Grounded )
			{
				Entity.Velocity = Entity.Velocity.WithZ( 100f );
				Entity.ApplyAbsoluteImpulse( ForwardDirection * 100f );
			}

			Wallrunning = side;
			previousWallrunSide = side;

			wallrunSinceJumping = true;

			return true;
		}

		return false;
	}

	bool CheckForWall( bool isWallrunningOnRightSide, bool behind = false )
	{
		var leftDirection = Entity.Velocity.EulerAngles.ToRotation().Left;
		var forwardDirection = Entity.Velocity.EulerAngles.ToRotation().Forward;

		var from = Entity.Position + Vector3.Up * 50f;
		var to = Entity.Position + Vector3.Up * 50f + leftDirection * (isWallrunningOnRightSide ? -30f : 30f) + forwardDirection * (behind ? -15f : 15f);


		if ( debugMode )
			DebugOverlay.Line( start: from, end: to, duration: 1f );

		var trace = Trace.Ray( from, to ).Run();

		return trace.Hit;
	}
}
