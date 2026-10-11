using System;

namespace GoldSrc;

public static class ScenePhysics
{
	public const string Tag = "goldsrc";
	public const string ShadowTag = "shadow";
	public const string WorldTag = "world";
	public const string BoxTag = "bbox";

	private const float DistEpsilon = 0.03125f;

	public static bool Sweep( Scene scene, Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, out float fraction, out SceneTraceResult result )
	{
		fraction = 1f;
		result = default;

		if ( scene is null )
			return false;

		var trace = mins == maxs
			? scene.Trace.Ray( start + mins, end + mins )
			: scene.Trace.Box( new BBox( mins, maxs ), start, end );

		result = trace.WithoutTags( Tag, ClientRagdoll.Tag ).Run();

		if ( !result.Hit )
			return false;

		if ( result.StartedSolid )
		{
			fraction = 0f;
			return true;
		}

		var approach = -Vector3.Dot( end - start, result.Normal );

		fraction = approach > 0f ? MathF.Max( result.Fraction - (DistEpsilon / approach), 0f ) : result.Fraction;

		return true;
	}
}
