using GoldSrc.Bsp;
using Plane = GoldSrc.Bsp.Plane;

namespace GoldSrc;

partial class Client
{
	private sealed class Frustum
	{
		public readonly Plane[] Planes = new Plane[4];
		public Vector3 Origin;
		public Vector3 Forward;
	}

	private static readonly Vector3 StudioDefaultMins = new( -16f, -16f, -16f );
	private static readonly Vector3 StudioDefaultMaxs = new( 16f, 16f, 16f );

	private readonly Frustum _nextViewFrustum = new();

	[ConVar( "r_cullsequencebox" )]
	public static float CullSequenceBox { get; set; } = 1f;

	public bool StudioCheckBBox( Studio.File header, ref EntityState state, Vector3 origin, Transform transform )
	{
		var studio = header.Header;
		Vector3 mins;
		Vector3 maxs;

		if ( !MathLib.VectorCompare( Vector3.Zero, studio.BBMin ) )
		{
			mins = origin + studio.BBMin;
			maxs = origin + studio.BBMax;
		}
		else if ( !MathLib.VectorCompare( Vector3.Zero, studio.Min ) )
		{
			mins = origin + studio.Min;
			maxs = origin + studio.Max;
		}
		else
		{
			mins = origin + StudioDefaultMins;
			maxs = origin + StudioDefaultMaxs;
		}

		if ( state.Sequence >= header.Sequences.Length )
			state.Sequence = 0;

		ref readonly var sequence = ref header.Sequences[state.Sequence];

		for ( var i = 0; i < 8; i++ )
		{
			var corner = transform.PointToWorld( new Vector3(
				(i & 1) != 0 ? sequence.BBMin.x : sequence.BBMax.x,
				(i & 2) != 0 ? sequence.BBMin.y : sequence.BBMax.y,
				(i & 4) != 0 ? sequence.BBMin.z : sequence.BBMax.z ) );

			mins = Vector3.Min( mins, corner );
			maxs = Vector3.Max( maxs, corner );
		}

		return BoxInView( _frustum, mins, maxs ) || (NextViewCamera is not null && BoxInView( _nextViewFrustum, mins, maxs ));
	}

	private bool BoxInView( Frustum frustum, Vector3 mins, Vector3 maxs )
	{
		if ( !IsSinglePlayerGame && CullSequenceBox != 0f )
			return !CullBox( frustum, mins, maxs );

		var plane = new Plane( frustum.Forward, Vector3.Dot( frustum.Origin, frustum.Forward ), PlaneType.AnyZ );

		return Bsp.File.BoxOnPlaneSide( mins, maxs, plane ) != 2;
	}
}
