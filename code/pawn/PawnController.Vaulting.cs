using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	void UpdateVault()
	{
		// TODO: make smoother

		Vector3 pos = Bezier.Approach(
			start: VaultStartPos,
			end: VaultTargetPos,
			curveSize: 30f,
			t: bezierCounter
		);

		bezierCounter += (vaultSpeed / 100) * Time.Delta;

		Entity.Position = Entity.Position.LerpTo( pos, Time.Delta * 50f );

		if ( bezierCounter >= 1.0f )
			Vaulting = 0;
	}

	void TryVaulting()
	{
		if ( Grounded && !Input.Down( "forward" ) )
			return;

		float speed = Entity.Velocity.Length;

		if ( speed.AlmostEqual( 0f ) )
			return;

		float rayDistance = Math.Max( (speed / 500) * 60f, 35f );

		float showDebugTime = 3f;
		float boxRadius = 20f;
		var distanceBehindObstacle = rayDistance * 1.20f + 60f;

		BBox boxFront = new BBox( center: 0, size: 35f )
			.Translate( Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 30f );

		var traceFront = Trace.Box(
			bbox: boxFront,
			from: 0, to: 0
		).Run();

		if ( debugMode )
			DebugOverlay.Box(
				bounds: boxFront,
				color: Color.Red,
				duration: showDebugTime
			);

		if ( !traceFront.Hit )
			return;

		BBox boxBehindObstacle = new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 70f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Right * boxRadius
		).Translate( Entity.Position + Entity.Rotation.Forward * distanceBehindObstacle );

		if ( debugMode )
			DebugOverlay.Box( bounds: boxBehindObstacle, color: Color.Blue, duration: showDebugTime );

		var traceBehindObstacle = Trace.Box(
			bbox: boxBehindObstacle,
			from: 0, to: 0
		).Run();

		// Make sure we are not vaulting inside map geometry
		var traceWallFailsafe = Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 60f,
			to: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle
		).Run();

		var hitFailsafe = traceWallFailsafe.Entity?.IsValid == true; //bool? needs to be converted to bool

		if ( debugMode )
			DebugOverlay.Line(
				start: Entity.Position + Entity.Rotation.Up * 60f,
				end: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle,
				duration: showDebugTime
			);

		if ( !traceBehindObstacle.Hit && !hitFailsafe )
		{
			// Vault over

			// Check if there's space to vault over
			var topBoxSmall = new BBox(
				mins: Vector3.Forward * +boxRadius * 0.70f + Vector3.Up * 50f + Vector3.Left * boxRadius * 0.70f,
				maxs: Vector3.Forward * -boxRadius * 0.70f + Vector3.Up * 80f + Vector3.Right * boxRadius * 0.70f
			).Translate( Entity.Position + Entity.Rotation.Forward * rayDistance );

			var traceBoxSmallAboveObstacle = Trace.Box(
				bbox: topBoxSmall,
				from: 0, to: 0
			).Run();

			if ( debugMode )
				DebugOverlay.Box( bounds: topBoxSmall, color: Color.Green, duration: showDebugTime );

			if ( traceBoxSmallAboveObstacle.Hit )
				return;

			// Cast a ray to check where the ground is
			var traceObstacleSurface = Trace.Ray(
				from: boxBehindObstacle.Center + Entity.Rotation.Up * 30f,
				to: boxBehindObstacle.Center + Entity.Rotation.Up * -50f
			).Run();

			if ( debugMode )
				DebugOverlay.Line(
					start: boxBehindObstacle.Center + Entity.Rotation.Up * 30f,
					end: boxBehindObstacle.Center + Entity.Rotation.Up * -50f,
					color: Color.Blue, duration: showDebugTime
				);

			if ( traceObstacleSurface.Hit )
			{
				var groundPosition = traceObstacleSurface.HitPosition;

				VaultTargetPos = groundPosition;
			}
			else
			{
				VaultTargetPos = boxBehindObstacle.Center + Entity.Rotation.Up * -40f;
			}

			Vaulting = 2;
			vaultSpeed = 200f;
		}
		else
		{
			// Vault onto

			// Make sure there's enough space to stand on obstacle
			var topBoxLarge = new BBox(
				mins: Vector3.Forward * +boxRadius + Vector3.Up * 45f + Vector3.Left * boxRadius,
				maxs: Vector3.Forward * -boxRadius + Vector3.Up * 120f + Vector3.Right * boxRadius
			).Translate( Entity.Position + Entity.Rotation.Forward * rayDistance );

			var traceBoxLargeAboveObstacle = Trace.Box(
				bbox: topBoxLarge,
				from: 0, to: 0
			).Run();

			if ( debugMode )
				DebugOverlay.Box( bounds: topBoxLarge, color: Color.Green, duration: showDebugTime );

			if ( traceBoxLargeAboveObstacle.Hit )
				return;

			// Cast a ray to check where the ground is
			var traceObstacleSurface = Trace.Ray(
				from: topBoxLarge.Center + Entity.Rotation.Up * 30f,
				to: topBoxLarge.Center + Entity.Rotation.Up * -60f
			).Run();

			if ( debugMode )
				DebugOverlay.Line(
					start: topBoxLarge.Center + Entity.Rotation.Up * 30f,
					end: topBoxLarge.Center + Entity.Rotation.Up * -60f,
					color: Color.Blue, duration: showDebugTime
				);

			if ( !traceObstacleSurface.Hit )
				return;

			var groundPosition = traceObstacleSurface.HitPosition;

			VaultTargetPos = groundPosition + Vector3.Up * 13f;
			Vaulting = 1;
			vaultSpeed = Math.Max( Entity.Velocity.Length, 200f );
		}

		parkouredSinceJumping = true;
		parkouredBeforeLanding = true;

		VaultStartPos = Entity.Position;
		bezierCounter = 0f;

		var vaultDirection = (VaultTargetPos - Entity.Position).WithZ( 0 ).Normal;
		var speedAfterVault = Entity.Velocity.WithZ( 0 ).Length;

		Entity.Velocity = vaultDirection * speedAfterVault;
	}
}
