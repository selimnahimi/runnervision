using System;
using Sandbox;

namespace RunnerVision;

public class FaithVoice
{
	public int CurrentStrain { get; set; }
	public int MaxStrain => 100;
	private PawnController Controller { get; set; }

	public FaithVoice( PawnController controller )
	{
		Controller = controller;
	}

	public void Update()
	{
		
	}

	public bool IsInActivity()
	{
		if ( Controller.IsClimbing() ) return true;
		if ( Controller.IsDashing() ) return true;
		if ( Controller.IsWallRunning() ) return true;
		if ( Controller.IsRunning() ) return true;

		return false;
	}
}
