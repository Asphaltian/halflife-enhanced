using System;
using System.Buffers.Binary;

namespace GoldSrc.Studio;

public struct LatchedVars
{
	public float PrevAnimTime;
	public float SequenceTime;
	public byte PrevSeqBlending0;
	public byte PrevSeqBlending1;
	public Vector3 PrevOrigin;
	public Vector3 PrevAngles;
	public int PrevSequence;
	public float PrevFrame;
	public byte[] PrevController;
	public byte PrevBlending0;
	public byte PrevBlending1;
}

public sealed partial class BoneSetup
{
	public const int MaxStudioBones = 128;
	public const int MaxStudioControllers = 8;

	private const int AnimSize = 12;

	public Vector3[] Positions { get; } = new Vector3[MaxStudioBones];
	public Rotation[] Rotations { get; } = new Rotation[MaxStudioBones];

	private readonly Vector3[] _pos2 = new Vector3[MaxStudioBones];
	private readonly Rotation[] _q2 = new Rotation[MaxStudioBones];
	private readonly Vector3[] _pos3 = new Vector3[MaxStudioBones];
	private readonly Rotation[] _q3 = new Rotation[MaxStudioBones];
	private readonly Vector3[] _pos4 = new Vector3[MaxStudioBones];
	private readonly Rotation[] _q4 = new Rotation[MaxStudioBones];
	private readonly Vector3[] _pos1b = new Vector3[MaxStudioBones];
	private readonly Rotation[] _q1b = new Rotation[MaxStudioBones];
	private readonly float[] _adj = new float[MaxStudioControllers];

	private File _header;
	private EntityState _state;
	private LatchedVars _latched;
	private double _time;
	private bool _doInterp;
	private byte _mouthOpen;

	public void SetupBones( File header, ref EntityState state, ref LatchedVars latched, double time, bool doInterp, byte mouthOpen = 0, PlayerInfo playerInfo = null )
	{
		_header = header;
		_state = state;
		_latched = latched;
		_time = time;
		_doInterp = doInterp;
		_mouthOpen = mouthOpen;

		if ( state.Sequence >= header.Sequences.Length )
			state.Sequence = _state.Sequence = 0;

		ref readonly var sequence = ref header.Sequences[_state.Sequence];

		var f = EstimateFrame( sequence, _state, time, doInterp );
		var numBones = header.Bones.Length;

		CalcRotations( Positions, Rotations, sequence, 0, f );

		if ( sequence.NumBlends > 1 )
		{
			CalcRotations( _pos2, _q2, sequence, 1, f );

			var dadt = EstimateInterpolant();
			var s = (float)(((_state.Blending0 * dadt) + (_latched.PrevBlending0 * (1.0 - dadt))) / 255.0);

			SlerpBones( Rotations, Positions, _q2, _pos2, s );

			if ( sequence.NumBlends == 4 )
			{
				CalcRotations( _pos3, _q3, sequence, 2, f );
				CalcRotations( _pos4, _q4, sequence, 3, f );

				s = (float)(((_state.Blending0 * dadt) + (_latched.PrevBlending0 * (1.0 - dadt))) / 255.0);
				SlerpBones( _q3, _pos3, _q4, _pos4, s );

				s = (float)(((_state.Blending1 * dadt) + (_latched.PrevBlending1 * (1.0 - dadt))) / 255.0);
				SlerpBones( Rotations, Positions, _q3, _pos3, s );
			}
		}

		if ( doInterp && _latched.SequenceTime != 0f && _latched.SequenceTime + 0.2 > time && _latched.PrevSequence < header.Sequences.Length )
		{
			ref readonly var prevSequence = ref header.Sequences[_latched.PrevSequence];

			CalcRotations( _pos1b, _q1b, prevSequence, 0, _latched.PrevFrame );

			if ( prevSequence.NumBlends > 1 )
			{
				CalcRotations( _pos2, _q2, prevSequence, 1, _latched.PrevFrame );

				var s = (float)(_latched.PrevSeqBlending0 / 255.0);
				SlerpBones( _q1b, _pos1b, _q2, _pos2, s );

				if ( prevSequence.NumBlends == 4 )
				{
					CalcRotations( _pos3, _q3, prevSequence, 2, _latched.PrevFrame );
					CalcRotations( _pos4, _q4, prevSequence, 3, _latched.PrevFrame );

					s = (float)(_latched.PrevSeqBlending0 / 255.0);
					SlerpBones( _q3, _pos3, _q4, _pos4, s );

					s = (float)(_latched.PrevSeqBlending1 / 255.0);
					SlerpBones( _q1b, _pos1b, _q3, _pos3, s );
				}
			}

			var blend = (float)(1.0 - ((time - _latched.SequenceTime) / 0.2));
			SlerpBones( Rotations, Positions, _q1b, _pos1b, blend );
		}
		else
		{
			latched.PrevFrame = (float)f;
		}

		if ( playerInfo is null )
			return;

		if ( playerInfo.GaitSequence >= header.Sequences.Length )
			playerInfo.GaitSequence = 0;

		if ( playerInfo.GaitSequence == 0 )
			return;

		ref readonly var gaitSequence = ref header.Sequences[playerInfo.GaitSequence];

		CalcRotations( _pos2, _q2, gaitSequence, 0, playerInfo.GaitFrame );

		var copy = true;

		for ( var i = 0; i < numBones; i++ )
		{
			if ( header.Bones[i].Name == "Bip01 Spine" )
				copy = false;
			else if ( header.Bones[i].Parent >= 0 && header.Bones[header.Bones[i].Parent].Name == "Bip01 Pelvis" )
				copy = true;

			if ( !copy )
				continue;

			Positions[i] = _pos2[i];
			Rotations[i] = _q2[i];
		}
	}

	public void MergeBones( File header, ref EntityState state, double time )
	{
		_header = header;
		_state = state;
		_time = time;
		_doInterp = Client.StudioInterpolation;

		if ( state.Sequence >= header.Sequences.Length )
			state.Sequence = _state.Sequence = 0;

		ref readonly var sequence = ref header.Sequences[_state.Sequence];

		CalcRotations( Positions, Rotations, sequence, 0, EstimateFrame( sequence, _state, time, Client.StudioInterpolation ) );
	}

	public static double EstimateFrame( in SequenceDesc sequence, in EntityState state, double time, bool doInterp )
	{
		double dfdt;
		double f;

		if ( doInterp && time >= state.AnimTime )
			dfdt = (time - state.AnimTime) * state.FrameRate * sequence.Fps;
		else
			dfdt = 0;

		if ( sequence.NumFrames <= 1 )
			f = 0;
		else
			f = state.Frame * (sequence.NumFrames - 1) / 256.0;

		f += dfdt;

		if ( (sequence.Flags & SequenceFlags.Looping) == SequenceFlags.Looping )
		{
			if ( sequence.NumFrames > 1 )
				f -= (int)(f / (sequence.NumFrames - 1)) * (sequence.NumFrames - 1);

			if ( f < 0 )
				f += sequence.NumFrames - 1;
		}
		else
		{
			if ( f >= sequence.NumFrames - 1.001 )
				f = sequence.NumFrames - 1.001;

			if ( f < 0.0 )
				f = 0.0;
		}

		return f;
	}

	private float EstimateInterpolant()
	{
		var dadt = 1f;

		if ( _doInterp && _state.AnimTime >= _latched.PrevAnimTime + 0.01 )
		{
			dadt = (float)((_time - _state.AnimTime) / 0.1);

			if ( dadt > 2f )
				dadt = 2f;
		}

		return dadt;
	}

	private bool IsRecomputed( in SequenceDesc sequence, int bone )
	{
		return _header.IsControlled( bone ) || (bone == sequence.MotionBone && (sequence.MotionType & (MotionFlags.X | MotionFlags.Y | MotionFlags.Z)) != 0);
	}

	private void CalcRotations( Vector3[] pos, Rotation[] q, in SequenceDesc sequence, int blend, double frameTime )
	{
		var f = (float)frameTime;

		if ( f > sequence.NumFrames - 1 )
			f = 0f;
		else if ( f < -0.01 )
			f = -0.01f;

		var frame = (int)f;
		var dadt = EstimateInterpolant();
		var s = f - frame;

		CalcBoneAdj( dadt );

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

		if ( (sequence.MotionType & MotionFlags.X) != 0 )
			pos[sequence.MotionBone].x = 0f;

		if ( (sequence.MotionType & MotionFlags.Y) != 0 )
			pos[sequence.MotionBone].y = 0f;

		if ( (sequence.MotionType & MotionFlags.Z) != 0 )
			pos[sequence.MotionBone].z = 0f;
	}

	private void CalcBoneAdj( float dadt )
	{
		var controllers = _header.BoneControllers;
		var controller2 = _latched.PrevController;

		for ( var j = 0; j < controllers.Length; j++ )
		{
			var i = controllers[j].Index;
			float value;

			if ( i <= 3 )
			{
				if ( (controllers[j].Type & MotionFlags.RLoop) != 0 )
				{
					if ( Math.Abs( _state.GetController( i ) - controller2[i] ) > 128 )
					{
						var a = (_state.GetController( j ) + 128) % 256;
						var b = (controller2[j] + 128) % 256;

						value = (float)(((a * dadt) + (b * (1 - dadt)) - 128) * (360.0 / 256.0) + controllers[j].Start);
					}
					else
					{
						value = (float)(((_state.GetController( i ) * dadt) + (controller2[i] * (1.0 - dadt))) * (360.0 / 256.0) + controllers[j].Start);
					}
				}
				else
				{
					value = (float)(((_state.GetController( i ) * dadt) + (controller2[i] * (1.0 - dadt))) / 255.0);
					value = Math.Clamp( value, 0f, 1f );
					value = (float)(((1.0 - value) * controllers[j].Start) + (value * controllers[j].End));
				}
			}
			else
			{
				value = (float)(_mouthOpen / 64.0);
				if ( value > 1f )
					value = 1f;

				value = (float)(((1.0 - value) * controllers[j].Start) + (value * controllers[j].End));
			}

			switch ( controllers[j].Type & MotionFlags.Types )
			{
				case MotionFlags.XR:
				case MotionFlags.YR:
				case MotionFlags.ZR:
					_adj[j] = (float)(value * (Math.PI / 180.0));
					break;
				case MotionFlags.X:
				case MotionFlags.Y:
				case MotionFlags.Z:
					_adj[j] = value;
					break;
			}
		}
	}

	private Rotation CalcBoneQuaternion( int frame, float s, in Bone bone, ReadOnlySpan<byte> anim )
	{
		Vector3 angle1 = default;
		Vector3 angle2 = default;

		for ( var j = 0; j < 3; j++ )
		{
			var offset = BinaryPrimitives.ReadUInt16LittleEndian( anim[((j + 3) * 2)..] );

			if ( offset == 0 )
			{
				angle2[j] = angle1[j] = bone.Value[j + 3];
			}
			else
			{
				var values = anim[offset..];
				var k = frame;

				if ( Total( values ) < Valid( values ) )
					k = 0;

				while ( Total( values ) <= k )
				{
					k -= Total( values );
					values = values[((Valid( values ) + 1) * 2)..];

					if ( Total( values ) < Valid( values ) )
						k = 0;
				}

				if ( Valid( values ) > k )
				{
					angle1[j] = Value( values, k + 1 );

					if ( Valid( values ) > k + 1 )
						angle2[j] = Value( values, k + 2 );
					else if ( Total( values ) > k + 1 )
						angle2[j] = angle1[j];
					else
						angle2[j] = Value( values, Valid( values ) + 2 );
				}
				else
				{
					angle1[j] = Value( values, Valid( values ) );

					if ( Total( values ) > k + 1 )
						angle2[j] = angle1[j];
					else
						angle2[j] = Value( values, Valid( values ) + 2 );
				}

				angle1[j] = bone.Value[j + 3] + (angle1[j] * bone.Scale[j + 3]);
				angle2[j] = bone.Value[j + 3] + (angle2[j] * bone.Scale[j + 3]);
			}

			if ( bone.BoneController[j + 3] != -1 )
			{
				angle1[j] += _adj[bone.BoneController[j + 3]];
				angle2[j] += _adj[bone.BoneController[j + 3]];
			}
		}

		if ( angle1.x != angle2.x || angle1.y != angle2.y || angle1.z != angle2.z )
			return QuaternionSlerp( AngleQuaternion( angle1 ), AngleQuaternion( angle2 ), s );

		return AngleQuaternion( angle1 );
	}

	private Vector3 CalcBonePosition( int frame, float s, in Bone bone, ReadOnlySpan<byte> anim )
	{
		Vector3 pos = default;

		for ( var j = 0; j < 3; j++ )
		{
			pos[j] = bone.Value[j];

			var offset = BinaryPrimitives.ReadUInt16LittleEndian( anim[(j * 2)..] );

			if ( offset != 0 )
			{
				var values = anim[offset..];
				var k = frame;

				if ( Total( values ) < Valid( values ) )
					k = 0;

				while ( Total( values ) <= k )
				{
					k -= Total( values );
					values = values[((Valid( values ) + 1) * 2)..];

					if ( Total( values ) < Valid( values ) )
						k = 0;
				}

				if ( Valid( values ) > k )
				{
					if ( Valid( values ) > k + 1 )
						pos[j] = (float)((((Value( values, k + 1 ) * (1.0 - s)) + (s * Value( values, k + 2 ))) * bone.Scale[j]) + pos[j]);
					else
						pos[j] += Value( values, k + 1 ) * bone.Scale[j];
				}
				else
				{
					if ( Total( values ) <= k + 1 )
						pos[j] = (float)((((Value( values, Valid( values ) ) * (1.0 - s)) + (s * Value( values, Valid( values ) + 2 ))) * bone.Scale[j]) + pos[j]);
					else
						pos[j] += Value( values, Valid( values ) ) * bone.Scale[j];
				}
			}

			if ( bone.BoneController[j] != -1 )
				pos[j] += _adj[bone.BoneController[j]];
		}

		return pos;
	}

	private void SlerpBones( Rotation[] q1, Vector3[] pos1, Rotation[] q2, Vector3[] pos2, float s )
	{
		s = Math.Clamp( s, 0f, 1f );

		var s1 = (float)(1.0 - s);

		for ( var i = 0; i < _header.Bones.Length; i++ )
		{
			q1[i] = QuaternionSlerp( q1[i], q2[i], s );
			pos1[i] = (pos1[i] * s1) + (pos2[i] * s);
		}
	}

	private static byte Valid( ReadOnlySpan<byte> values ) => values[0];

	private static byte Total( ReadOnlySpan<byte> values ) => values[1];

	private static short Value( ReadOnlySpan<byte> values, int index ) => BinaryPrimitives.ReadInt16LittleEndian( values[(index * 2)..] );

	public static Rotation AngleQuaternion( Vector3 angles )
	{
		var angle = angles.z * 0.5;
		var sy = (float)Math.Sin( angle );
		var cy = (float)Math.Cos( angle );

		angle = angles.y * 0.5;
		var sp = (float)Math.Sin( angle );
		var cp = (float)Math.Cos( angle );

		angle = angles.x * 0.5;
		var sr = (float)Math.Sin( angle );
		var cr = (float)Math.Cos( angle );

		return new Rotation
		{
			x = (sr * cp * cy) - (cr * sp * sy),
			y = (cr * sp * cy) + (sr * cp * sy),
			z = (cr * cp * sy) - (sr * sp * cy),
			w = (cr * cp * cy) + (sr * sp * sy)
		};
	}

	public static Rotation QuaternionSlerp( Rotation p, Rotation q, float t )
	{
		var a = ((p.x - q.x) * (p.x - q.x)) + ((p.y - q.y) * (p.y - q.y)) + ((p.z - q.z) * (p.z - q.z)) + ((p.w - q.w) * (p.w - q.w));
		var b = ((p.x + q.x) * (p.x + q.x)) + ((p.y + q.y) * (p.y + q.y)) + ((p.z + q.z) * (p.z + q.z)) + ((p.w + q.w) * (p.w + q.w));

		if ( a > b )
			q = new Rotation { x = -q.x, y = -q.y, z = -q.z, w = -q.w };

		var cosom = (p.x * q.x) + (p.y * q.y) + (p.z * q.z) + (p.w * q.w);
		float sclp;
		float sclq;

		if ( 1.0 + cosom > 0.000001 )
		{
			if ( 1.0 - cosom > 0.000001 )
			{
				var omega = (float)Math.Acos( cosom );
				var sinom = (float)Math.Sin( omega );

				sclp = (float)(Math.Sin( (1.0 - t) * omega ) / sinom);
				sclq = (float)(Math.Sin( t * omega ) / sinom);
			}
			else
			{
				sclp = (float)(1.0 - t);
				sclq = t;
			}

			return new Rotation
			{
				x = (sclp * p.x) + (sclq * q.x),
				y = (sclp * p.y) + (sclq * q.y),
				z = (sclp * p.z) + (sclq * q.z),
				w = (sclp * p.w) + (sclq * q.w)
			};
		}

		sclp = (float)Math.Sin( (1.0 - t) * (0.5 * Math.PI) );
		sclq = (float)Math.Sin( t * (0.5 * Math.PI) );

		return new Rotation
		{
			x = (sclp * p.x) + (sclq * -q.y),
			y = (sclp * p.y) + (sclq * q.x),
			z = (sclp * p.z) + (sclq * -q.w),
			w = q.z
		};
	}
}
