using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public sealed class StudioHulls
{
	public const int TraceHitBox = 0x200;

	private const int MaxHulls = 128;

	private readonly Studio.BoneSetup _boneSetup = new();
	private readonly Hull[] _hulls = CreateHulls();
	private readonly int[] _hitGroups = new int[MaxHulls];
	private readonly int[] _bones = new int[MaxHulls];

	public Hull this[int index] => _hulls[index];

	public int HitGroup( int index ) => _hitGroups[index];

	public int Bone( int index ) => _bones[index];

	private static Hull[] CreateHulls()
	{
		var hulls = new Hull[MaxHulls];

		for ( var i = 0; i < hulls.Length; i++ )
			hulls[i] = Hull.CreateBox();

		return hulls;
	}

	public int Build( Studio.File header, float frame, int sequence, Vector3 angles, Vector3 origin, Vector3 size, byte[] controller, byte[] blending )
	{
		_boneSetup.ServerSetupBones( header, frame, sequence, angles with { x = -angles.x }, origin, controller, blending, -1 );

		var hitBoxes = header.HitBoxes;

		for ( var i = 0; i < hitBoxes.Length; i++ )
		{
			ref readonly var box = ref hitBoxes[i];

			var transform = _boneSetup.ServerBoneTransform( box.Bone );
			var planes = _hulls[i].Planes;

			_hitGroups[i] = box.Group;
			_bones[i] = box.Bone;

			for ( var axis = 0; axis < 3; axis++ )
			{
				var normal = new Vector3( transform[axis], transform[4 + axis], transform[8 + axis] );
				var distance = (normal.y * transform[7]) + (normal.x * transform[3]) + (normal.z * transform[11]);
				var expand = Math.Abs( (double)(normal.y * size.y) ) + Math.Abs( (double)(normal.x * size.x) ) + Math.Abs( (double)(normal.z * size.z) );

				planes[axis * 2] = new Bsp.Plane( normal, (float)(expand + (distance + box.BBMax[axis])), PlaneType.AnyZ );
				planes[(axis * 2) + 1] = new Bsp.Plane( normal, (float)((distance + box.BBMin[axis]) - expand), PlaneType.AnyZ );
			}
		}

		return hitBoxes.Length;
	}
}
