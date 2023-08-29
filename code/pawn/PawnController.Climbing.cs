using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	void UpdateClimbing()
	{
		Climbing = false;

		if ( IsWallRunning() || IsVaulting() || Grounded )
		{
			TimeSinceClimbing = 0f;
			CurrentClimbAmount = 0;
			return;
		}

		if ( !Input.Down( "jump" ) )
			return;

		if ( Entity.Velocity.z < 0 )
			return;

		if ( CurrentClimbAmount >= MaxClimbAmount )
			return;

		var traceFront = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 15f,
			to: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 25f
		).Run();

		if ( debugMode )
			DebugOverlay.Line(
				start: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 15f,
				end: Entity.Position + Entity.Rotation.Up * 50f + Entity.Rotation.Forward * 25f
			);

		if ( traceFront.Hit )
		{
			Climbing = true;
		}

		if ( traceFront.Hit && TimeSinceClimbing > 0.15f )
		{
			Log.Info( traceFront.Entity );
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
