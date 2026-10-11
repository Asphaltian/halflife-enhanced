using System;

namespace GoldSrc;

partial class Client
{
	private const int FadeOut = 0x0001;
	private const int FadeModulate = 0x0002;
	private const int FadeStayOut = 0x0004;
	private const int FadeLongFade = 0x0008;

	private struct ScreenFade
	{
		public float Speed;
		public float End;
		public float TotalEnd;
		public float Reset;
		public byte R;
		public byte G;
		public byte B;
		public byte Alpha;
		public int Flags;
	}

	private ScreenFade _fade;

	private void InitLevel()
	{
		_shake = default;
		_fade = default;

		if ( Cvar.GetFloat( "v_dark" ) == 0f )
			return;

		_fade.Reset = (float)(ClientTime + 5.0);
		_fade.Alpha = 255;
		_fade.Speed = 51f;
		_fade.End = _fade.Reset + 5f;

		Cvar.Set( "v_dark", "0" );
	}

	private bool ScreenFadeMessage( string name, byte[] data )
	{
		var duration = BitConverter.ToUInt16( data, 0 );
		var holdTime = BitConverter.ToUInt16( data, 2 );
		var flags = BitConverter.ToInt16( data, 4 );
		var scale = (flags & FadeLongFade) != 0 ? 256f : 4096f;

		_fade.End = duration * (1f / scale);
		_fade.Reset = holdTime * (1f / scale);
		_fade.R = data[6];
		_fade.G = data[7];
		_fade.B = data[8];
		_fade.Alpha = data[9];
		_fade.Flags = flags;
		_fade.Speed = 0f;

		if ( duration == 0 )
			return true;

		if ( (flags & FadeOut) != 0 )
		{
			if ( _fade.End != 0f )
				_fade.Speed = -(float)_fade.Alpha / _fade.End;

			_fade.End = (float)(_fade.End + ClientTime);
			_fade.TotalEnd = _fade.End;
			_fade.Reset += _fade.End;
		}
		else
		{
			if ( _fade.End != 0f )
				_fade.Speed = _fade.Alpha / _fade.End;

			_fade.Reset = (float)(_fade.Reset + ClientTime);
			_fade.End += _fade.Reset;
		}

		return true;
	}

	private int FadeAlpha()
	{
		var stayOut = (_fade.Flags & FadeStayOut) != 0;

		if ( ClientTime > _fade.Reset && ClientTime > _fade.End && !stayOut )
			return 0;

		double alpha;

		if ( stayOut )
		{
			if ( (_fade.Flags & FadeOut) == 0 || _fade.TotalEnd <= ClientTime )
			{
				_fade.End = (float)(ClientTime + 0.1);
				return _fade.Alpha;
			}

			alpha = ((_fade.TotalEnd - ClientTime) * _fade.Speed) + _fade.Alpha;
		}
		else
		{
			alpha = (_fade.End - ClientTime) * _fade.Speed;

			if ( (_fade.Flags & FadeOut) != 0 )
				alpha += _fade.Alpha;
		}

		return Math.Clamp( (int)alpha, 0, _fade.Alpha );
	}

	private void PolyBlend()
	{
		var alpha = FadeAlpha();

		if ( alpha == 0 )
			return;

		if ( (_fade.Flags & FadeModulate) == 0 )
		{
			_hudDraws.Add( new HudDraw( HudDrawMode.Blend, null, 0, 0, ScreenWidth, ScreenHeight, 0, 0, new Color32( _fade.R, _fade.G, _fade.B, (byte)alpha ), null ) );
			return;
		}

		var r = (byte)((((_fade.R - 255) * alpha) + (255 * 255)) >> 8);
		var g = (byte)((((_fade.G - 255) * alpha) + (255 * 255)) >> 8);
		var b = (byte)((((_fade.B - 255) * alpha) + (255 * 255)) >> 8);

		_hudDraws.Add( new HudDraw( HudDrawMode.Modulate, null, 0, 0, ScreenWidth, ScreenHeight, 0, 0, new Color32( r, g, b, 255 ), null ) );
	}
}
