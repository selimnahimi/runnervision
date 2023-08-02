using Sandbox;

namespace MyGame;

public partial class WeaponViewModel : BaseViewModel
{
	protected Weapon Weapon { get; init; }

	public WeaponViewModel( Weapon weapon )
	{
		Weapon = weapon;
		EnableShadowCasting = false;
		EnableViewmodelRendering = true;
	}

	public override void PlaceViewmodel()
	{
		base.PlaceViewmodel();
		Rotation = Rotation.RotateAroundAxis( Vector3.Left, -Camera.Rotation.Pitch() );

		Camera.Main.SetViewModelCamera( 90f, 1, 500 );
	}

	public override void FrameSimulate( IClient cl )
	{
		LocalRotation = LocalRotation.RotateAroundAxis( Vector3.Left, 100f );
	}
}
