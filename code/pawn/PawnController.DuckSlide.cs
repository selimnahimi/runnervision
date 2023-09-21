using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RunnerVision;

public partial class PawnController
{
	public void TryDucking()
	{
		Log.Info( "Duck" );
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
}
