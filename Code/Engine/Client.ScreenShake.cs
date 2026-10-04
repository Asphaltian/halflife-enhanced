using System;

namespace GoldSrc;

partial class Client
{
	private struct ScreenShake
	{
		public float Time;
		public float Duration;
		public float Amplitude;
		public float Frequency;
		public float NextShake;
		public Vector3 Offset;
		public float Angle;
		public Vector3 AppliedOffset;
		public float AppliedAngle;
	}

	private ScreenShake _shake;

	private bool ScreenShakeMessage( string name, byte[] data )
	{
		var amplitude = BitConverter.ToUInt16( data, 0 ) * (1f / 4096f);
		var duration = BitConverter.ToUInt16( data, 2 ) * (1f / 4096f);

		_shake.Duration = duration;
		_shake.Time = (float)(duration + ClientTime);

		if ( _shake.Amplitude < amplitude )
			_shake.Amplitude = amplitude;

		_shake.NextShake = 0f;
		_shake.Frequency = BitConverter.ToUInt16( data, 4 ) * (1f / 256f);

		return true;
	}

	public void CalcShake()
	{
		if ( ClientTime > _shake.Time || _shake.Duration <= 0f || _shake.Amplitude <= 0f || _shake.Frequency <= 0f )
		{
			if ( _shake.Time != 0f )
			{
				_shake.Time = 0f;
				_shake.AppliedOffset = Vector3.Zero;
				_shake.AppliedAngle = 0f;
			}

			return;
		}

		var frameTime = ClientTime - _oldTime;

		if ( ClientTime > _shake.NextShake )
		{
			_shake.NextShake = (float)((_shake.Duration / _shake.Frequency) + ClientTime);

			var x = Server.RandomFloat( -_shake.Amplitude, _shake.Amplitude );
			var y = Server.RandomFloat( -_shake.Amplitude, _shake.Amplitude );
			var z = Server.RandomFloat( -_shake.Amplitude, _shake.Amplitude );

			_shake.Offset = new Vector3( x, y, z );
			_shake.Angle = Server.RandomFloat( -_shake.Amplitude * 0.25f, _shake.Amplitude * 0.25f );
		}

		var fraction = (float)((ClientTime - _shake.Time) / _shake.Duration);
		var frequency = fraction != 0f ? _shake.Frequency / fraction * _shake.Frequency : 0f;

		fraction = (float)(Math.Sin( frequency * ClientTime ) * (fraction * fraction));

		_shake.AppliedOffset = _shake.Offset * fraction;
		_shake.AppliedAngle = _shake.Angle * fraction;
		_shake.Amplitude -= (float)frameTime / (_shake.Frequency * _shake.Duration) * _shake.Amplitude;
	}

	public void ApplyShake( ref Vector3 origin, ref Vector3 angles, float factor )
	{
		origin += _shake.AppliedOffset * factor;
		angles.z += factor * _shake.AppliedAngle;
	}
}
