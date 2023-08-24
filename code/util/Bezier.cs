using System;

namespace RunnerVision;

public class Bezier
{
	// Source: https://programmerbay.com/c-program-to-draw-bezier-curve-using-4-control-points/
	public static Vector3 Approach( Vector3 start, Vector3 end, float curveSize, float t )
	{
		var cp1 = start + Vector3.Up * curveSize;
		var cp2 = end + Vector3.Up * curveSize;
		float x = (float)(Math.Pow( 1 - t, 3 ) * start.x + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.x + 3 * t * t * (1 - t) * cp2.x + Math.Pow( t, 3 ) * end.x);
		float y = (float)(Math.Pow( 1 - t, 3 ) * start.y + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.y + 3 * t * t * (1 - t) * cp2.y + Math.Pow( t, 3 ) * end.y);
		float z = (float)(Math.Pow( 1 - t, 3 ) * start.z + 3 * t * Math.Pow( 1 - t, 2 ) * cp1.z + 3 * t * t * (1 - t) * cp2.z + Math.Pow( t, 3 ) * end.z);

		return new Vector3( x, y, z );
	}
}
