using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Numerics;

namespace RunnerVision;

public partial class Hands : Weapon
{
	public override string ModelPath => "models/faith_v2.vmdl";
	public override string ViewModelPath => "models/faith_v2.vmdl";

	private bool jumping = false;
	private float yaw = 0.0f;

	[ClientRpc]
	protected virtual void ShootEffects()
	{
		Game.AssertClient();

		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );

		Pawn.SetAnimParameter( "b_attack", true );
		ViewModelEntity?.SetAnimParameter( "fire", true );
	}

	public override void PrimaryAttack()
	{
	}

	protected override void Animate()
	{
		Pawn.SetAnimParameter( "holdtype", (int)CitizenAnimationHelper.HoldTypes.Pistol );
	}

	public override void FrameSimulate( IClient cl )
	{
		base.FrameSimulate( cl );
	}

	public override void Simulate( IClient player )
	{
		base.Simulate( player );
	}
}
