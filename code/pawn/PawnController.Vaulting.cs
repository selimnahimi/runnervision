using Sandbox;
using System;

namespace RunnerVision;

public partial class PawnController
{
	public enum VaultType
	{
		None,
		Onto,
		Over,
		OntoHigh
	}

	float showDebugTime => 3f;
	float boxRadius => 20f;

	void InitiateVault()
	{
		var speed = GetSpeed();
		var rayDistance = GetRayDistance( speed );

		if ( !CanVault( speed, rayDistance ) )
			return;

		bool successfulVault = TryVaulting( rayDistance );

		if ( successfulVault )
			SetupVault();
	}

	bool IsVaulting()
	{
		return Vaulting != VaultType.None;
	}

	void ProgressVault()
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

	void SetupVault()
	{
		parkouredSinceJumping = true;
		parkouredBeforeLanding = true;

		VaultStartPos = Entity.Position;
		bezierCounter = 0f;

		var vaultDirection = (VaultTargetPos - Entity.Position).WithZ( 0 ).Normal;
		var speedAfterVault = Entity.Velocity.WithZ( 0 ).Length;

		Entity.Velocity = vaultDirection * speedAfterVault;
	}

	bool CanVault(float speed, float rayDistance)
	{
		if ( Grounded && !Input.Down( "forward" ) )
			return false;

		BBox boxFront = GetBoxFront( rayDistance );

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
			return false;

		return true;
	}

	float GetSpeed()
	{
		return Entity.Velocity.Length;
	}

	float GetRayDistance(float speed)
	{
		return Math.Max( (speed / 500) * 60f, 35f );
	}

	bool TryVaulting(float rayDistance)
	{
		var distanceBehindObstacle = rayDistance * 1.20f + 60f;
		BBox boxBehindObstacle = GetBoxBehindObstacle( distanceBehindObstacle );

		bool successfulVault = false;

		if ( ShouldVaultOntoHigh() )
		{
			Log.Info( "HI" );
		}
		else if ( ShouldVaultOver( boxBehindObstacle, distanceBehindObstacle ) )
		{
			successfulVault = TryVaultOver( rayDistance, boxBehindObstacle );
		}
		else if ( ShouldVaultOnto() )
		{
			successfulVault = TryVaultOnto( rayDistance );
		}

		return successfulVault;
	}

	bool ShouldVaultOntoHigh()
	{
		if ( !IsClimbing() )
			return false;

		BBox boxAboveWall = GetBoxAboveWall();
		DebugOverlay.Box( bounds: boxAboveWall, Color.Red );

		return true;
	}

	bool ShouldVaultOnto()
	{
		return true;
	}

	bool ShouldVaultOver(BBox boxBehindObstacle, float distanceBehindObstacle)
	{
		if ( debugMode )
			DebugOverlay.Box( bounds: boxBehindObstacle, color: Color.Blue, duration: showDebugTime );

		var traceBehindObstacle = Trace.Box(
			bbox: boxBehindObstacle,
			from: 0, to: 0
		).Run();

		// Make sure we are not vaulting inside map geometry
		var traceWallFailsafe = GetTraceWallFailSafe( distanceBehindObstacle );

		var hitFailsafe = traceWallFailsafe.Entity?.IsValid == true; //bool? needs to be converted to bool

		if ( debugMode )
			DebugOverlay.Line(
				start: Entity.Position + Entity.Rotation.Up * 60f,
				end: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle,
				duration: showDebugTime
			);

		return !traceBehindObstacle.Hit && !hitFailsafe;
	}

	bool TryVaultOver(float rayDistance, BBox boxBehindObstacle)
	{
		if ( CanVaultOver( rayDistance ) )
			VaultOver( boxBehindObstacle );
		else
			return false;

		return true;
	}

	bool TryVaultOnto(float rayDistance)
	{
		// Make sure there's enough space to stand on obstacle
		var topBoxLarge = GetTopBoxLarge( rayDistance );

		if ( !CanVaultOnto( topBoxLarge ) )
			return false;

		var groundPosition = VaultOntoGetGround( topBoxLarge );

		if ( groundPosition != Vector3.Zero )
			VaultOnto( groundPosition );
		else
			return false;

		return true;
	}

	TraceResult GetTraceWallFailSafe( float distanceBehindObstacle )
	{
		return Trace.Ray(
			from: Entity.Position + Entity.Rotation.Up * 60f,
			to: Entity.Position + Entity.Rotation.Up * 60f + Entity.Rotation.Forward * distanceBehindObstacle
		).Run();
	}

	BBox GetBoxAboveWall()
	{
		return new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 45f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Up * 120f + Vector3.Right * boxRadius
		).Translate( Entity.Position + Entity.Rotation.Forward * 50f );
	}

	BBox GetBoxFront( float rayDistance )
	{
		return new BBox( center: 0, size: 35f )
			.Translate( Entity.Position + Entity.Rotation.Forward * rayDistance + Entity.Rotation.Up * 30f );
	}

	BBox GetTopBoxLarge(float rayDistance)
	{
		return new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 45f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Up * 120f + Vector3.Right * boxRadius
		).Translate( Entity.Position + Entity.Rotation.Forward * rayDistance );
	}

	BBox GetBoxBehindObstacle( float distanceBehindObstacle )
	{
		return new BBox(
			mins: Vector3.Forward * +boxRadius + Vector3.Up * 70f + Vector3.Left * boxRadius,
			maxs: Vector3.Forward * -boxRadius + Vector3.Right * boxRadius
		).Translate( Entity.Position + Entity.Rotation.Forward * distanceBehindObstacle );
	}

	bool CanVaultOver( float rayDistance )
	{
		if ( GetSpeed().AlmostEqual( 0f ) )
			return false;

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

		return !traceBoxSmallAboveObstacle.Hit;
	}

	void VaultOver( BBox boxBehindObstacle )
	{
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

		Vaulting = VaultType.Over;
		vaultSpeed = 200f;
	}

	bool CanVaultOnto( BBox topBoxLarge )
	{
		if ( GetSpeed().AlmostEqual( 0f ) )
			return false;

		var traceBoxLargeAboveObstacle = Trace.Box(
			bbox: topBoxLarge,
			from: 0, to: 0
		).Run();

		if ( debugMode )
			DebugOverlay.Box( bounds: topBoxLarge, color: Color.Green, duration: showDebugTime );

		return !traceBoxLargeAboveObstacle.Hit;
	}

	Vector3 VaultOntoGetGround( BBox topBoxLarge )
	{
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
			return Vector3.Zero;

		return traceObstacleSurface.HitPosition;
	}

	void VaultOnto( Vector3 groundPosition )
	{
		VaultTargetPos = groundPosition + Vector3.Up * 13f;
		Vaulting = VaultType.Onto;
		vaultSpeed = Math.Max( Entity.Velocity.Length, 200f );
	}
}
