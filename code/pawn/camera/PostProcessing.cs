using Sandbox;
using Sandbox.Effects;
using System;

namespace RunnerVision
{
	public class CameraPostProcessing : ScreenEffects
	{
		public float PawnMaxSpeed { get; set; }

		public override void OnFrame( SceneCamera target )
		{
			base.OnFrame( target );

			UpdateChromaticAberration();
		}

		private void UpdateChromaticAberration()
		{
			ChromaticAberration.Scale = ChromaticAberration.Scale.LerpTo( Math.Max( 0f, PawnMaxSpeed / 2000f - 0.5f ), 0.5f * Time.Delta );
		}
	}
}
