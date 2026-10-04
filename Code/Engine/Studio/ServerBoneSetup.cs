using System;

namespace GoldSrc.Studio;

partial class BoneSetup
{
	private readonly float[][] _boneTransform = CreateTransforms();
	private readonly float[] _rotationMatrix = new float[12];
	private readonly float[] _boneMatrix = new float[12];
	private readonly int[] _chain = new int[MaxStudioBones];

	private static float[][] CreateTransforms()
	{
		var transforms = new float[MaxStudioBones][];

		for ( var i = 0; i < MaxStudioBones; i++ )
			transforms[i] = new float[12];

		return transforms;
	}

	public void ServerSetupBones( File header, float frame, int sequence, Vector3 angles, Vector3 origin, byte[] controller, byte[] blending, int bone )
	{
		if ( sequence < 0 || sequence >= header.Sequences.Length )
		{
			Log.Info( $"SV_StudioSetupBones:  sequence {sequence}/{header.Sequences.Length} out of range for model {header.Header.Name}" );
			sequence = 0;
		}

		_header = header;
		_state = new EntityState { Controller0 = controller[0], Controller1 = controller[1], Controller2 = controller[2], Controller3 = controller[3] };
		_latched = new LatchedVars { PrevController = controller };

		ref readonly var sequenceDesc = ref header.Sequences[sequence];

		var bones = header.Bones;
		var chainLength = 0;

		if ( bone < -1 || bone >= bones.Length )
			bone = 0;

		if ( bone == -1 )
		{
			chainLength = bones.Length;

			for ( var i = 0; i < bones.Length; i++ )
				_chain[bones.Length - i - 1] = i;
		}
		else
		{
			do
			{
				_chain[chainLength++] = bone;
				bone = bones[bone].Parent;
			}
			while ( bone != -1 );
		}

		var f = sequenceDesc.NumFrames > 1 ? (sequenceDesc.NumFrames - 1) * frame * (1f / 256f) : 0f;
		var frameIndex = (int)f;
		var s = f - frameIndex;

		CalcBoneAdj( 0f );

		ServerCalcRotations( Positions, Rotations, sequenceDesc, 0, f, frameIndex, s );

		if ( sequenceDesc.NumBlends > 1 )
		{
			ServerCalcRotations( _pos2, _q2, sequenceDesc, 1, f, frameIndex, s );
			SlerpBones( Rotations, Positions, _q2, _pos2, blending[0] / 255f );
		}

		AngleMatrix( angles, _rotationMatrix );

		_rotationMatrix[3] = origin.x;
		_rotationMatrix[7] = origin.y;
		_rotationMatrix[11] = origin.z;

		for ( var i = chainLength - 1; i >= 0; i-- )
		{
			var index = _chain[i];
			var parent = bones[index].Parent;

			QuaternionMatrix( Rotations[index], _boneMatrix );

			_boneMatrix[3] = Positions[index].x;
			_boneMatrix[7] = Positions[index].y;
			_boneMatrix[11] = Positions[index].z;

			ConcatTransforms( parent == -1 ? _rotationMatrix : _boneTransform[parent], _boneMatrix, _boneTransform[index] );
		}
	}

	private void ServerCalcRotations( Vector3[] pos, Rotation[] q, in SequenceDesc sequence, int blend, float f, int frame, float s )
	{
		_header.Sample( sequence, blend, f, pos, q );

		var anim = _header.GetAnim( sequence )[(blend * _header.Bones.Length * AnimSize)..];

		for ( var i = 0; i < _header.Bones.Length; i++ )
		{
			if ( !IsRecomputed( sequence, i ) )
				continue;

			var boneAnim = anim[(i * AnimSize)..];

			q[i] = CalcBoneQuaternion( frame, s, _header.Bones[i], boneAnim );
			pos[i] = CalcBonePosition( frame, s, _header.Bones[i], boneAnim );
		}
	}

	public float[] ServerBoneTransform( int bone ) => _boneTransform[bone];

	public Vector3 ServerBonePosition( int bone )
	{
		var transform = _boneTransform[bone];

		return new Vector3( transform[3], transform[7], transform[11] );
	}

	public Vector3 ServerTransformPoint( int bone, Vector3 point )
	{
		var transform = _boneTransform[bone];

		return new Vector3(
			(point.x * transform[0]) + (point.y * transform[1]) + (point.z * transform[2]) + transform[3],
			(point.x * transform[4]) + (point.y * transform[5]) + (point.z * transform[6]) + transform[7],
			(point.x * transform[8]) + (point.y * transform[9]) + (point.z * transform[10]) + transform[11] );
	}

	private const double DegreesToRadians = 0.017453292519943295;

	private static void AngleMatrix( Vector3 angles, float[] matrix )
	{
		var angle = (double)(float)(angles.y * DegreesToRadians);
		var sy = (float)Math.Sin( angle );
		var cy = (float)Math.Cos( angle );

		angle = (float)(angles.x * DegreesToRadians);

		var sp = (float)Math.Sin( angle );
		var cp = (float)Math.Cos( angle );

		angle = (float)(angles.z * DegreesToRadians);

		var sr = (float)Math.Sin( angle );
		var cr = (float)Math.Cos( angle );

		matrix[0] = cp * cy;
		matrix[4] = cp * sy;
		matrix[8] = -sp;
		matrix[1] = (sr * sp * cy) - (cr * sy);
		matrix[5] = (sr * sp * sy) + (cr * cy);
		matrix[9] = sr * cp;
		matrix[2] = (cr * sp * cy) - (-sy * sr);
		matrix[6] = (cr * sp * sy) - (sr * cy);
		matrix[10] = cr * cp;
		matrix[3] = 0f;
		matrix[7] = 0f;
		matrix[11] = 0f;
	}

	private static void QuaternionMatrix( Rotation q, float[] matrix )
	{
		double x = q.x;
		double y = q.y;
		double z = q.z;
		double w = q.w;

		matrix[0] = (float)(1.0 - (2.0 * y * y) - (2.0 * z * z));
		matrix[4] = (float)((2.0 * x * y) + (2.0 * w * z));
		matrix[8] = (float)((2.0 * x * z) - (2.0 * w * y));

		matrix[1] = (float)((2.0 * x * y) - (2.0 * w * z));
		matrix[5] = (float)(1.0 - (2.0 * x * x) - (2.0 * z * z));
		matrix[9] = (float)((2.0 * y * z) + (2.0 * w * x));

		matrix[2] = (float)((2.0 * x * z) + (2.0 * w * y));
		matrix[6] = (float)((2.0 * y * z) - (2.0 * w * x));
		matrix[10] = (float)(1.0 - (2.0 * x * x) - (2.0 * y * y));
	}

	private static void ConcatTransforms( float[] in1, float[] in2, float[] output )
	{
		for ( var row = 0; row < 3; row++ )
		{
			var r = row * 4;

			output[r] = (in1[r] * in2[0]) + (in1[r + 1] * in2[4]) + (in1[r + 2] * in2[8]);
			output[r + 1] = (in1[r] * in2[1]) + (in1[r + 1] * in2[5]) + (in1[r + 2] * in2[9]);
			output[r + 2] = (in1[r] * in2[2]) + (in1[r + 1] * in2[6]) + (in1[r + 2] * in2[10]);
			output[r + 3] = (in1[r] * in2[3]) + (in1[r + 1] * in2[7]) + (in1[r + 2] * in2[11]) + in1[r + 3];
		}
	}
}
