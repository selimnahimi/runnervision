using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RunnerVision;

public partial class PawnController
{
	private Sound slideSoundLoop;

	public void TryDucking()
	{
		Ducking = true;
	}

	public void StopDucking()
	{
		CurrentMaxSpeed = 1000f;
		Ducking = false;
	}

	public bool IsDucking()
	{
		return Ducking;
	}

	public void UpdateDuck()
	{
		if ( IsDucking() )
		{
			CurrentMaxSpeed = 450f;
		}
	}

	public void TrySliding()
	{
		if ( ShouldSlide() )
		{
			InitiateSlide();
		}
	}

	public bool ShouldSlide()
	{
		if ( !Grounded )
			return false;

		if ( !IsDucking() )
			return false;

		if ( !IsSliding() && GetHorizontalSpeed() < 100f )
			return false;

		if ( IsSliding() && GetHorizontalSpeed() < 100f )
			return false;

		return true;
	}

	public void InitiateSlide()
	{
		Entity.ApplyAbsoluteImpulse( Entity.Rotation.Forward * 100f );
		PlaySlideSounds();

		Sliding = true;
	}

	public void UpdateSlide()
	{
		if ( IsSliding() )
		{
			if ( !ShouldSlide() )
				StopSliding();
		}
		else
		{
			TrySliding();
		}

		if ( slideSoundLoop.IsPlaying && !IsSliding() )
		{
			slideSoundLoop.SetVolume( 1.0f - TimeSinceSlideStopped * 2 );

			if ( TimeSinceSlideStopped > 0.5f )
			{
				slideSoundLoop.Stop();
			}
		}
	}

	public bool IsSliding()
	{
		return Sliding;
	}

	public void StopSliding()
	{
		Sliding = false;

		TimeSinceSlideStopped = 0f;
	}

	public void PlaySlideSounds()
	{
		PlaySlideStart();
		PlaySlideLoop();
	}

	public void PlaySlideStart()
	{
		Sound.FromWorld( "concretefootstepslidestart", Entity.Position + Vector3.Down * 10f );
	}

	public void PlaySlideLoop()
	{
		slideSoundLoop.Stop();
		slideSoundLoop = Entity.PlaySound( "concretefootstepslideloop" );
	}
}
