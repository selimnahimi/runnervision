using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Numerics;

namespace MyGame;

public partial class Hands : Weapon
{
	public override string ModelPath => "models/faith.vmdl";
	public override string ViewModelPath => "models/faith.vmdl";

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
		ViewModelEntity?.SetAnimParameter( "speed", Pawn.Velocity.Length );
		// Log.Info( Pawn.Velocity.Length );
		// Log.Info( ViewModelEntity?.GetAnimParameterFloat( "speed" ) );
	}

	public override void FrameSimulate( IClient cl )
	{
		base.FrameSimulate( cl );
	}

	public override void Simulate( IClient player )
	{
		base.Simulate( player );

		ViewModelEntity?.SetAnimParameter( "jumping", jumping );

		if ( Pawn.GroundEntity != null )
		{
			jumping = false;
			Pawn.Controller.Wallrunning = 0;
		}

		if ( Input.Pressed( "jump" ) && Pawn.GroundEntity != null )
		{
			jumping = true;

			if ( CheckForWallLeft() )
			{
				// ViewModelEntity?.SetAnimParameter( "wallrunning", 1 );
				// Pawn.CameraTilt = 10f;
				Pawn.Controller.Wallrunning = 1;
			}
			else if ( CheckForWallRight() )
			{
				Pawn.Controller.Wallrunning = 2;
			}
		}

		ViewModelEntity?.SetAnimParameter( "wallrunning", Pawn.Controller.Wallrunning );

		Log.Info( "Left: " + CheckForWallLeft() );
		Log.Info( "Right: " + CheckForWallRight() );
	}

	bool CheckForWallLeft()
	{
		var trace = Trace.Ray( Pawn.Position, Pawn.Position + Pawn.Rotation.Left * 30f ).Run();

		return trace.Hit && trace.Entity.IsWorld;
	}

	bool CheckForWallRight()
	{
		var trace = Trace.Ray( Pawn.Position, Pawn.Position + Pawn.Rotation.Right * 30f ).Run();

		return trace.Hit && trace.Entity.IsWorld;
	}
}
