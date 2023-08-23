using Sandbox;
using Sandbox.Html;
using Sandbox.UI;
using System;

public class MenuCity : ScenePanel
{
	public CameraMode CameraController { get; set; }
	public float TimeScale { get; set; } = 1.0f;

	public MenuCity()
	{
		World = new SceneWorld();
		var model = Sandbox.Model.Load( "models/glasscity.vmdl" );

		new SceneModel( World, model, Transform.Zero );
		CameraController = new Orbit( Vector3.Zero, new Vector3(0f, 0f, 0f), 10f );

		var skyMat = Material.Load( "materials/menu_city/city_sky.vmat" );
		new SceneSkyBox( World, skyMat );
		var cubemapTex = Texture.Load( "textures/cubemaps/default.vtex" );
		new SceneCubemap ( World, cubemapTex, new BBox(Vector3.Zero, 50f));

		// new SceneSpotLight( World, Vector3.Up * 10f, Color.Yellow );
		new SceneLight( World, Vector3.Right * 100f + Vector3.Up * 20f, 1000f, Color.White );
	}

	public override void Tick()
	{
		base.Tick();

		if ( !IsVisible ) return;

		CameraController?.Update( this );
	}

	public class CameraMode
	{
		public virtual void Update( MenuCity mc )
		{

		}
	}

	public class Orbit : CameraMode
	{
		public Vector3 Center;
		public Vector3 Offset;
		public Angles Angles;
		public float Distance;

		public Vector2 PitchLimit = new Vector2( -90, 90 );
		public Vector2 YawLimit = new Vector2( -360, 360 );

		public Angles HomeAngles;
		public Vector3 SpinVelocity;

		public float TimeSinceCenterChange;
		public bool ChangingCenter = false;
		public Vector3 NewCenter;
		public float Pitch = 5f;

		public Orbit( Vector3 center, Vector3 normal, float distance )
		{
			Center = center;
			HomeAngles = Rotation.LookAt( normal.Normal ).Angles();
			Angles = HomeAngles;
			Distance = distance;
			SpinVelocity = new Vector3(0f, 2f, 0f);
			TimeSinceCenterChange = 0f;
		}

		public override void Update( MenuCity mc )
		{
			Angles.pitch += SpinVelocity.x * Time.Delta;
			Angles.yaw += SpinVelocity.y * Time.Delta;

			Angles.roll = 0;
			Angles.pitch = Angles.pitch.LerpTo( Pitch, 0.01f );

			Angles = Angles.Normal;

			//Angles.pitch = Angles.pitch.Clamp( PitchLimit.x, PitchLimit.y );
			//Angles.yaw = Angles.yaw.Clamp( YawLimit.x, YawLimit.y );

			if ( TimeSinceCenterChange > 8f )
			{
				var x = Random.Shared.Float( -25f, 25f );
				var y = Random.Shared.Float( -25f, 25f );
				var pitch = Random.Shared.Float( 2f, 5f );

				NewCenter = new Vector3(x, y, 0f) + (mc.Camera.Rotation.Backward * Distance) + Offset;
				Pitch = pitch;

				ChangingCenter = true;
				TimeSinceCenterChange = 0f;
			}

			if (Center.DistanceSquared(NewCenter) > 100f)
			{
				SpinVelocity = SpinVelocity.LerpTo( new Vector3( 0f, 15f, 0f ), 0.1f );
			}
			else
			{
				SpinVelocity = SpinVelocity.LerpTo( new Vector3( 0f, 2f, 0f ), 0.01f );
			}

			Center = Center.LerpTo( NewCenter, 0.01f );
			

			mc.Camera.Position = Center + (mc.Camera.Rotation.Backward * Distance) + Offset;
			mc.Camera.Rotation = Rotation.From( Angles );

			TimeSinceCenterChange += Time.Delta;
		}
	}
}
