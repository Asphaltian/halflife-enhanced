using System;

namespace GoldSrc.TrueType;

public static class Fixed
{
	public static int Mul26Dot6( int a, int b )
	{
		if ( a is < 0xB505 and > -0xB505 && b is < 0xB505 and > -0xB505 )
			return ((a * b) + 0x20) >> 6;

		var negative = (a < 0) != (b < 0);
		var magnitude = (uint)((((ulong)(uint)Math.Abs( (long)a ) * (uint)Math.Abs( (long)b )) + 0x20) >> 6);

		return (int)(negative ? 0 - magnitude : magnitude);
	}

	public static int FixMul( int a, int b )
	{
		var product = (long)a * b;

		return Saturate( (product + 0x8000L + (product >> 63)) >> 16 );
	}

	public static int FixDiv( int a, int b )
	{
		if ( b == 0 )
			return 0x7FFFFFFF;

		var numerator = (long)a << 16;
		long half = b / 2;

		if ( (b < 0) != (numerator < 0) )
			half = -half;

		return Saturate( (numerator + half) / b );
	}

	public static int RoundedDivide( long numerator, int denominator )
	{
		long half = denominator / 2;

		if ( (denominator < 0) != (numerator < 0) )
			half = -half;

		return (int)((numerator + half) / denominator);
	}

	public static int Saturate( long value )
	{
		if ( value >= 0x80000000L )
			return 0x7FFFFFFF;

		return value < -0x80000000L ? unchecked((int)0x80000000) : (int)value;
	}

	public static int PixRound( int value ) => (value + 32) & -64;

	public static int PixFloor( int value ) => value & -64;

	public static int PixCeil( int value ) => (value + 63) & -64;
}

public sealed class FontScale
{
	private readonly int _fixedScale;
	private readonly int _numerator;
	private readonly int _denominator;
	private readonly int _shift = -1;
	private readonly bool _exact;

	public FontScale( int numerator, int denominator )
	{
		var lowZeros = 0;

		for ( var bits = (uint)(numerator | denominator); bits != 0 && (bits & 1) == 0; bits >>= 1 )
			lowZeros++;

		lowZeros--;

		if ( lowZeros >= 1 )
		{
			numerator >>= lowZeros;
			denominator >>= lowZeros;
		}

		if ( numerator < 0x2000000 )
			numerator <<= 6;
		else
			denominator >>= 6;

		_fixedScale = Fixed.FixDiv( numerator, denominator );

		if ( numerator >= 0x8000 )
			return;

		_exact = true;
		_numerator = numerator;
		_denominator = denominator;

		if ( denominator == 0 || (denominator & (denominator - 1)) != 0 )
			return;

		_shift = 0;

		for ( var bits = (uint)denominator; (bits & 1) == 0; bits >>= 1 )
			_shift++;
	}

	public int ApplyFixed( int value )
	{
		if ( _exact && _shift >= 0 && _numerator < 0x1FF )
			return ((value * _numerator) + (_denominator >> 1)) >> (_shift + 6);

		if ( !_exact || _shift >= 0 )
			return Fixed.FixMul( value, _fixedScale >> 6 );

		var product = (_numerator >> 6) * value;
		var half = _denominator >> 1;

		return value >= 0 ? (half + product) / _denominator : -((half - product) / _denominator);
	}

	public int Reverse( int value ) => Fixed.FixDiv( value, _fixedScale >> 6 );

	public int Apply( int value )
	{
		if ( !_exact )
			return Fixed.FixMul( value, _fixedScale );

		if ( _shift >= 0 )
			return ((_denominator >> 1) + (value * _numerator)) >> _shift;

		var product = value * _numerator;
		var half = _denominator >> 1;

		return value >= 0 ? (half + product) / _denominator : -((half - product) / _denominator);
	}
}

public sealed class Zone
{
	public const byte OnCurve = 0x01;
	public const byte TouchX = 0x08;
	public const byte TouchY = 0x10;

	public int PointCount;
	public int ContourCount;

	public int[] OrgX;
	public int[] OrgY;
	public int[] CurX;
	public int[] CurY;
	public int[] OrusX;
	public int[] OrusY;
	public byte[] Tags;
	public int[] Contours;

	public Zone( int maxPoints, int maxContours )
	{
		OrgX = new int[maxPoints];
		OrgY = new int[maxPoints];
		CurX = new int[maxPoints];
		CurY = new int[maxPoints];
		OrusX = new int[maxPoints];
		OrusY = new int[maxPoints];
		Tags = new byte[maxPoints];
		Contours = new int[maxContours];
	}

	public void Reset( int pointCount, int contourCount )
	{
		if ( pointCount > CurX.Length )
		{
			var size = pointCount + 32;

			OrgX = new int[size];
			OrgY = new int[size];
			CurX = new int[size];
			CurY = new int[size];
			OrusX = new int[size];
			OrusY = new int[size];
			Tags = new byte[size];
		}

		if ( contourCount > Contours.Length )
			Contours = new int[contourCount + 8];

		PointCount = pointCount;
		ContourCount = contourCount;

		Array.Clear( OrgX, 0, pointCount );
		Array.Clear( OrgY, 0, pointCount );
		Array.Clear( CurX, 0, pointCount );
		Array.Clear( CurY, 0, pointCount );
		Array.Clear( OrusX, 0, pointCount );
		Array.Clear( OrusY, 0, pointCount );
		Array.Clear( Tags, 0, pointCount );
	}
}

public sealed class Interpreter
{
	private const int RoundToHalfGrid = 0;
	private const int RoundToGrid = 1;
	private const int RoundToDoubleGrid = 2;
	private const int RoundDownToGrid = 3;
	private const int RoundUpToGrid = 4;
	private const int RoundOff = 5;
	private const int RoundSuper = 6;
	private const int RoundSuper45 = 7;

	private const int RasterizerVersion = 42;

	private struct GraphicsState
	{
		public int Rp0;
		public int Rp1;
		public int Rp2;
		public int DualX;
		public int DualY;
		public int ProjX;
		public int ProjY;
		public int FreeX;
		public int FreeY;
		public int Loop;
		public int MinimumDistance;
		public int RoundState;
		public bool AutoFlip;
		public int ControlValueCutIn;
		public int SingleWidthCutIn;
		public int SingleWidthValue;
		public int DeltaBase;
		public int DeltaShift;
		public int InstructControl;
		public int ScanState;
		public int PfProj;
		public int Period;
		public int PeriodMask;
		public int Phase;
		public int Threshold;
		public int GridPeriod;
		public int Gep0;
		public int Gep1;
		public int Gep2;
	}

	private sealed class Definition
	{
		public byte[] Code;
		public int Start;
		public int End;
		public bool Active;
	}

	private struct CallRecord
	{
		public byte[] Code;
		public int CodeStart;
		public int CodeEnd;
		public int ReturnIp;
		public int Count;
		public Definition Definition;
	}

	private readonly int[] _stack;
	private readonly int[] _storage;
	private readonly Definition[] _functions;
	private readonly Definition[] _instructions = new Definition[256];
	private readonly CallRecord[] _calls = new CallRecord[CallBudget];
	private readonly Zone _twilight;

	private int[] _cvt = [];
	private GraphicsState _gs;
	private GraphicsState _defaultGs;

	private byte[] _code;
	private int _codeEnd;
	private int _ip;
	private int _top;
	private int _callTop;

	private int _pixelsPerEm;
	private int _pointSize;
	private FontScale _scale;
	private bool _composite;

	private const int InstructionBudget = 10000000;
	private const int JumpBudget = 10000;
	private const int CallBudget = 100;
	private const int DefaultScanControl = 0x120;

	private const int FontProgram = 0;
	private const int ControlProgram = 1;
	private const int GlyphProgram = 2;

	private readonly int _stackLimit;
	private readonly int _storageLimit;
	private readonly int _functionLimit;
	private readonly int _instructionLimit;
	private readonly int _twilightLimit;
	private readonly int _pointLimit;

	private int _phase;
	private int _codeStart;
	private int _instructionBudget;
	private int _jumpBudget;
	private int _callBudget;
	private int _instructionCount;

	public bool Failed { get; private set; }

	private Zone _zp0;
	private Zone _zp1;
	private Zone _zp2;

	public bool Grayscale { get; set; }

	public Zone GlyphZone { get; }

	public int ScanKind
	{
		get
		{
			var control = _gs.ScanState & 0xFFFF;
			var threshold = control & 0xFF;

			if ( (control & 0x100) == 0 || (threshold < _pixelsPerEm && threshold != 0xFF) )
				return 2;

			return (_gs.ScanState >> 16) & 0xFFFF;
		}
	}

	public Interpreter( TrueTypeFont font )
	{
		_stackLimit = Math.Max( font.MaxStackElements, 1 );
		_storageLimit = font.MaxStorage;
		_functionLimit = font.MaxFunctionDefs;
		_instructionLimit = font.MaxInstructionDefs;
		_twilightLimit = Math.Min( font.MaxTwilightPoints, 0x7FFF );
		_pointLimit = Math.Min( font.MaxPoints, 0x7FF6 ) + 4;

		_stack = new int[_stackLimit];
		_storage = new int[_storageLimit];
		_functions = new Definition[_functionLimit];
		_twilight = new Zone( _twilightLimit, 1 );
		_twilight.PointCount = _twilightLimit;
		_twilight.ContourCount = 1;

		if ( _twilightLimit > 0 )
			_twilight.Contours[0] = _twilightLimit - 1;

		GlyphZone = new Zone( font.MaxPoints + 8, font.MaxContours + 4 );

		ResetGraphicsState();
	}

	private void ResetGraphicsState()
	{
		_gs = new GraphicsState
		{
			DualX = 0x4000,
			ProjX = 0x4000,
			FreeX = 0x4000,
			PfProj = 0x4000,
			Loop = 1,
			MinimumDistance = 64,
			RoundState = RoundToGrid,
			AutoFlip = true,
			ControlValueCutIn = 68,
			DeltaBase = 9,
			DeltaShift = 3,
			Gep0 = 1,
			Gep1 = 1,
			Gep2 = 1
		};

		_zp0 = _zp1 = _zp2 = GlyphZone;
	}

	public void RunFontProgram( byte[] code, int offset, int length )
	{
		ResetGraphicsState();

		Array.Clear( _functions );
		Array.Clear( _instructions );

		_instructionCount = 0;
		_instructionBudget = InstructionBudget;
		_phase = FontProgram;
		Failed = false;

		if ( _pixelsPerEm >= 2 )
			Execute( code, offset, length );
	}

	public void ResetBudget() => _instructionBudget = InstructionBudget;

	public void SetSize( int pixelsPerEm, int pointSize, FontScale scale, int[] cvt )
	{
		_pixelsPerEm = pixelsPerEm;
		_pointSize = pointSize;
		_scale = scale;
		_composite = false;
		_cvt = cvt;

		Array.Clear( _storage );
		Array.Clear( _twilight.OrgX );
		Array.Clear( _twilight.OrgY );
		Array.Clear( _twilight.CurX );
		Array.Clear( _twilight.CurY );
		Array.Clear( _twilight.Tags );

		GlyphZone.Reset( 0, 0 );

		ResetGraphicsState();
		_defaultGs = _gs;
	}

	public void RunControlProgram( byte[] code, int offset, int length )
	{
		ResetGraphicsState();

		_gs.ScanState = DefaultScanControl;
		_instructionBudget = InstructionBudget;
		_phase = ControlProgram;
		Execute( code, offset, length );

		_gs.DualX = _gs.ProjX = _gs.FreeX = 0x4000;
		_gs.DualY = _gs.ProjY = _gs.FreeY = 0;
		_gs.PfProj = 0x4000;
		_gs.Rp0 = _gs.Rp1 = _gs.Rp2 = 0;
		_gs.Gep0 = _gs.Gep1 = _gs.Gep2 = 1;
		_gs.Loop = 1;

		_defaultGs = _gs;
	}

	public bool GridFitInhibited => (_defaultGs.InstructControl & 1) != 0;

	public bool ScanStateChanged => _gs.ScanState != _defaultGs.ScanState;

	public void ResetGlyphState() => _gs = _defaultGs;

	public void RunGlyphProgram( byte[] code, int offset, int length, bool composite )
	{
		if ( (_defaultGs.InstructControl & 2) != 0 )
		{
			var control = _gs.InstructControl;
			var scanState = _gs.ScanState;

			ResetGraphicsState();
			_gs.InstructControl = control;
			_gs.ScanState = scanState;
		}

		_zp0 = _zp1 = _zp2 = GlyphZone;
		_composite = composite;
		_phase = GlyphProgram;

		if ( _pixelsPerEm >= 2 )
			Execute( code, offset, length );
	}

	private void Fail() => Failed = true;

	private bool CheckPoint( Zone zone, int point )
	{
		if ( zone == _twilight )
		{
			if ( point >= 0 && point < _twilightLimit )
				return true;
		}
		else if ( point >= 0 && point < _pointLimit && zone.ContourCount > 0 && point <= zone.Contours[zone.ContourCount - 1] + 4 )
		{
			return true;
		}

		Fail();

		return false;
	}

	private bool CheckCvt( int index )
	{
		if ( index >= 0 && index < _cvt.Length )
			return true;

		Fail();

		return false;
	}

	private bool CheckProgram()
	{
		if ( _phase == GlyphProgram || (_phase == FontProgram && _zp2 == _twilight) )
			return true;

		Fail();

		return false;
	}

	private int StackNeed( int opcode )
	{
		if ( opcode >= 0xE0 )
			return 2;

		if ( opcode >= 0xC0 )
			return 1;

		switch ( opcode )
		{
			case 0x0F:
				return 5;

			case 0x8A:
				return 3;

			case >= 0x06 and <= 0x0B:
			case 0x23:
			case 0x27:
			case 0x2A:
			case 0x3A:
			case 0x3B:
			case 0x3E:
			case 0x3F:
			case 0x42:
			case 0x44:
			case >= 0x48 and <= 0x4A:
			case >= 0x50 and <= 0x55:
			case 0x5A:
			case 0x5B:
			case >= 0x60 and <= 0x63:
			case 0x70:
			case 0x78:
			case 0x79:
			case 0x81:
			case 0x82:
			case 0x86:
			case 0x87:
			case 0x8B:
			case 0x8C:
			case 0x8E:
				return 2;

			case >= 0x10 and <= 0x17:
			case 0x1A:
			case >= 0x1C and <= 0x21:
			case 0x25:
			case 0x26:
			case 0x29:
			case 0x2B:
			case 0x2C:
			case 0x2E:
			case 0x2F:
			case >= 0x34 and <= 0x37:
			case 0x43:
			case >= 0x45 and <= 0x47:
			case 0x4F:
			case >= 0x56 and <= 0x58:
			case >= 0x5C and <= 0x5F:
			case >= 0x64 and <= 0x6F:
			case >= 0x71 and <= 0x77:
			case 0x7E:
			case 0x7F:
			case 0x85:
			case 0x88:
			case 0x89:
			case 0x8D:
				return 1;

			case 0x32:
			case 0x33:
			case 0x39:
			case 0x3C:
			case 0x80:
				return _gs.Loop;

			case 0x38:
				return _gs.Loop < 0 ? -1 : _gs.Loop + 1;

			default:
				return 0;
		}
	}

	private Zone ZoneFor( int index ) => index == 0 ? _twilight : GlyphZone;

	private int Pop() => _top > 0 ? _stack[--_top] : 0;

	private void Push( int value )
	{
		if ( _top >= _stackLimit )
		{
			Fail();
			return;
		}

		_stack[_top++] = value;
	}

	private static int VectorMul( int value, int component ) => (int)(((((long)value * component) >> 13) + 1) >> 1);

	private int CheckSingleWidth( int distance )
	{
		if ( _gs.SingleWidthCutIn == 0 )
			return distance;

		var width = _gs.SingleWidthValue;

		if ( distance < 0 )
			return Math.Abs( -distance - width ) < _gs.SingleWidthCutIn ? -width : distance;

		return Math.Abs( distance - width ) < _gs.SingleWidthCutIn ? width : distance;
	}

	private int ScaleOriginal( int distance ) => _composite ? distance : _scale.Apply( distance );

	private int Project( int dx, int dy ) => VectorMul( dx, _gs.ProjX ) + VectorMul( dy, _gs.ProjY );

	private int DualProject( int dx, int dy ) => VectorMul( dx, _gs.DualX ) + VectorMul( dy, _gs.DualY );

	private int FreeDotProj() => _gs.PfProj;

	private void CheckPfProj()
	{
		if ( _gs.PfProj is > -0x400 and < 0x400 )
			_gs.PfProj = _gs.PfProj < 0 ? -0x4000 : 0x4000;
	}

	private void ComputePfProj()
	{
		_gs.PfProj = (short)((short)(((_gs.FreeY * _gs.ProjY) + 0x2000) >> 14) + (short)(((_gs.FreeX * _gs.ProjX) + 0x2000) >> 14));

		CheckPfProj();
	}

	private void MovePoint( Zone zone, int point, int distance, bool touch = true )
	{
		FreeMove( distance, out var dx, out var dy );

		if ( _gs.FreeX != 0 )
		{
			zone.CurX[point] += dx;

			if ( touch )
				zone.Tags[point] |= Zone.TouchX;
		}

		if ( _gs.FreeY != 0 )
		{
			zone.CurY[point] += dy;

			if ( touch )
				zone.Tags[point] |= Zone.TouchY;
		}
	}

	private void FreeMove( int distance, out int dx, out int dy )
	{
		var dot = FreeDotProj();

		dx = 0;
		dy = 0;

		if ( dot == 0x4000 )
		{
			if ( _gs.FreeX != 0 )
				dx = VectorMul( distance, _gs.FreeX );

			if ( _gs.FreeY != 0 )
				dy = VectorMul( distance, _gs.FreeY );

			return;
		}

		if ( _gs.FreeX != 0 )
		{
			if ( dot == _gs.FreeX )
			{
				dx = distance;
			}
			else
			{
				var numerator = (long)_gs.FreeX * distance;

				numerator += dot < 0 ? -(dot / 2) : dot / 2;
				dx = (int)(numerator / dot);
			}
		}

		if ( _gs.FreeY != 0 )
			dy = dot == _gs.FreeY ? distance : Fixed.RoundedDivide( (long)_gs.FreeY * distance, dot );
	}

	private static int RoundGrid( int distance )
	{
		var value = distance < 0 ? -((0x20 - distance) & ~0x3F) : (distance + 0x20) & ~0x3F;

		return (value ^ distance) < 0 && distance != 0 ? 0 : value;
	}

	private int Round( int distance )
	{
		int value;

		switch ( _gs.RoundState )
		{
			case RoundToGrid:
				if ( distance >= 0 )
				{
					value = Fixed.PixRound( distance );
					return value < 0 ? 0 : value;
				}

				value = -Fixed.PixRound( -distance );
				return value > 0 ? 0 : value;

			case RoundToHalfGrid:
				if ( distance >= 0 )
				{
					value = Fixed.PixFloor( distance ) + 32;
					return value < 0 ? 32 : value;
				}

				value = -(Fixed.PixFloor( -distance ) + 32);
				return value > 0 ? -32 : value;

			case RoundToDoubleGrid:
				if ( distance >= 0 )
				{
					value = (distance + 16) & -32;
					return value < 0 ? 0 : value;
				}

				value = -((-distance + 16) & -32);
				return value > 0 ? 0 : value;

			case RoundDownToGrid:
				if ( distance >= 0 )
				{
					value = Fixed.PixFloor( distance );
					return value < 0 ? 0 : value;
				}

				value = -Fixed.PixFloor( -distance );
				return value > 0 ? 0 : value;

			case RoundUpToGrid:
				if ( distance >= 0 )
				{
					value = Fixed.PixCeil( distance );
					return value < 0 ? 0 : value;
				}

				value = -Fixed.PixCeil( -distance );
				return value > 0 ? 0 : value;

			case RoundSuper:
				if ( distance >= 0 )
				{
					value = ((_gs.Threshold - _gs.Phase + distance) & _gs.PeriodMask) + _gs.Phase;
					return value < 0 ? _gs.Phase : value;
				}

				value = -(((_gs.Threshold - _gs.Phase - distance) & _gs.PeriodMask) + _gs.Phase);
				return value > 0 ? -_gs.Phase : value;

			case RoundSuper45:
			{
				var offset = (short)(_gs.Threshold - _gs.Phase);

				if ( distance >= 0 )
				{
					value = VectorMul( ShortFracDiv( (short)(offset + (short)distance), (short)_gs.GridPeriod ) & ~0x3F, _gs.GridPeriod ) + (short)_gs.Phase;
					return value < 0 ? (short)_gs.Phase : value;
				}

				value = -(VectorMul( ShortFracDiv( (short)(offset - (short)distance), (short)_gs.GridPeriod ) & ~0x3F, _gs.GridPeriod ) + (short)_gs.Phase);
				return value > 0 ? -(short)_gs.Phase : value;
			}

			default:
				return distance;
		}
	}

	private static short ShortFracDiv( short numerator, short denominator )
	{
		return numerator == 0 || denominator == 0 ? (short)0 : (short)((numerator << 14) / denominator);
	}

	private void SetSuperRound( int selector )
	{
		int quarter;
		int half;
		int threeQuarters;

		switch ( selector & 0xC0 )
		{
			case 0x00:
				_gs.Period = 0x20;
				quarter = 8;
				half = 0x10;
				threeQuarters = 0x18;
				break;

			case 0x40:
				_gs.Period = 0x40;
				quarter = 0x10;
				half = 0x20;
				threeQuarters = 0x30;
				break;

			case 0x80:
				_gs.Period = 0x80;
				quarter = 0x20;
				half = 0x40;
				threeQuarters = 0x60;
				break;

			default:
				_gs.Period = 999;
				quarter = 0xFA;
				half = 500;
				threeQuarters = 0x2ED;
				break;
		}

		_gs.PeriodMask = -_gs.Period;

		_gs.Phase = (selector & 0x30) switch
		{
			0x10 => quarter,
			0x20 => half,
			0x30 => threeQuarters,
			_ => 0
		};

		_gs.Threshold = (selector & 0xF) == 0 ? _gs.Period - 1 : (short)(((((selector & 0xF) - 4) * _gs.Period) + 4) >> 3);
	}

	private void SetSuperRound45( int selector )
	{
		switch ( selector & 0xC0 )
		{
			case 0x00:
				_gs.GridPeriod = 0x16A3;
				_gs.Period = 0x17;
				break;

			case 0x40:
				_gs.GridPeriod = 0x2D47;
				_gs.Period = 0x2D;
				break;

			case 0x80:
				_gs.GridPeriod = 0x5A8E;
				_gs.Period = 0x5B;
				break;

			default:
				_gs.GridPeriod = 999;
				_gs.Period = 4;
				break;
		}

		var period = (short)_gs.Period;

		_gs.Phase = (selector & 0x30) switch
		{
			0x10 => (ushort)(period + 2) >> 2,
			0x20 => (ushort)(period + 1) >> 1,
			0x30 => (ushort)((period * 3) + 2) >> 2,
			_ => 0
		};

		_gs.Threshold = (selector & 0xF) == 0 ? (short)(period - 1) : (short)(((_gs.Period * ((selector & 0xF) - 4)) + 4) >> 3);
	}

	private static void Normalize( int x, int y, out int normalX, out int normalY )
	{
		if ( x == 0 && y == 0 )
		{
			normalX = 0x4000;
			normalY = 0;
			return;
		}

		uint squared;

		if ( x is < 0x7FFF and >= -0x7FFF && y is >= -0x7FFF and < 0x7FFF )
		{
			var shift = 15;

			for ( squared = (uint)((x * x) + (y * y)); (int)squared < 0x20000000; squared <<= 2 )
				shift++;

			x <<= shift;
			y <<= shift;
		}
		else
		{
			while ( x < 0x20000000 )
			{
				if ( x < -0x1FFFFFFF || y < -0x1FFFFFFF || y > 0x1FFFFFFF )
					break;

				y *= 2;
				x *= 2;
			}

			var squaredY = (long)y * y;
			var squaredX = (long)x * x;

			squared = (uint)(Fixed.Saturate( (squaredY + 0x20000000) >> 30 ) + Fixed.Saturate( (squaredX + 0x20000000) >> 30 ));
		}

		var length = FracSqrt( squared );

		normalX = NormalizeComponent( x, length );
		normalY = NormalizeComponent( y, length );
	}

	private static int NormalizeComponent( int value, int length )
	{
		long half = length / 2;

		if ( (length < 0) != (value < 0) )
			half = -half;

		var numerator = ((long)value << 30) + half;
		var quotient = length == 0 ? (numerator < 0 ? 0x80000000L : 0x7FFFFFFFL) : numerator / length;

		return (short)((uint)(Fixed.Saturate( quotient ) + 0x8000) >> 16);
	}

	private static int FracSqrt( uint value )
	{
		if ( (int)value < 0 )
			return unchecked((int)0x80000000);

		var bit = 0x10000000u;
		var root = value < 0x40000000u ? 0u : 0x40000000u;
		var remainder = value < 0x40000000u ? value : value + 0xC0000000u;
		uint last;

		do
		{
			last = remainder;

			if ( bit + root <= last )
			{
				last -= bit + root;
				root += bit * 2;
			}

			remainder = last * 2;
			bit >>= 1;
		}
		while ( bit != 0 );

		if ( root < remainder )
		{
			var difference = remainder - root;

			root++;
			last = (difference * 2) - 1;
		}
		else
		{
			last <<= 2;
		}

		return (int)(root + (root < last ? 1u : 0u));
	}

	private void SkipArguments( int opcode )
	{
		int length;

		switch ( opcode )
		{
			case 0x40:
			case 0x41:
				if ( _ip >= _codeEnd )
				{
					Fail();
					return;
				}

				length = opcode == 0x40 ? _code[_ip] + 1 : (_code[_ip] * 2) + 1;
				break;

			case >= 0xB0 and <= 0xB7:
				length = opcode - 0xB0 + 1;
				break;

			case >= 0xB8 and <= 0xBF:
				length = (opcode - 0xB8 + 1) * 2;
				break;

			default:
				return;
		}

		_ip = Math.Min( _ip + length, _codeEnd );
	}

	private void Skip( bool stopAtElse )
	{
		var depth = 1;

		while ( depth != 0 && _ip < _codeEnd && !Failed )
		{
			var opcode = _code[_ip++];

			if ( opcode == 0x59 )
				depth--;
			else if ( opcode == 0x58 )
				depth++;
			else if ( opcode == 0x1B && stopAtElse && depth == 1 )
				break;
			else
				SkipArguments( opcode );
		}

		if ( depth != 0 && _ip >= _codeEnd )
			Fail();
	}

	private void Jump( int offset )
	{
		if ( --_jumpBudget == 0 )
		{
			Fail();
			return;
		}

		var target = (long)_ip + offset - 1;

		if ( target < _codeStart )
		{
			Fail();
			return;
		}

		_ip = (int)Math.Min( target, _codeEnd );
	}

	private void Define( int opcode )
	{
		var index = Pop();
		Definition definition;

		if ( opcode == 0x2C )
		{
			if ( index < 0 || index >= _functionLimit || _phase == GlyphProgram )
			{
				Fail();
				return;
			}

			definition = _functions[index] = new Definition();
		}
		else
		{
			if ( _phase == GlyphProgram || (uint)index > 0xFF )
			{
				Fail();
				return;
			}

			definition = _instructions[index];

			if ( definition is null )
			{
				if ( _instructionCount >= _instructionLimit )
				{
					Fail();
					return;
				}

				_instructionCount++;
				definition = _instructions[index] = new Definition();
			}
		}

		definition.Code = _code;
		definition.Start = _ip;
		definition.Active = true;

		while ( _ip < _codeEnd )
		{
			var instruction = _code[_ip++];

			if ( instruction == 0x2D )
			{
				definition.End = _ip - 1;
				return;
			}

			SkipArguments( instruction );

			if ( Failed )
				return;
		}

		definition.End = _codeEnd;
		Fail();
	}

	private void Call( Definition definition, int count )
	{
		if ( definition is not { Active: true } || --_callBudget == 0 )
		{
			Fail();
			return;
		}

		if ( count <= 0 )
		{
			_callBudget++;
			return;
		}

		_calls[_callTop++] = new CallRecord { Code = _code, CodeStart = _codeStart, CodeEnd = _codeEnd, ReturnIp = _ip, Count = count, Definition = definition };

		_code = definition.Code;
		_codeStart = definition.Start;
		_codeEnd = definition.End;
		_ip = definition.Start;
	}

	private void Execute( byte[] code, int offset, int length )
	{
		_code = code;
		_codeStart = offset;
		_codeEnd = offset + length;
		_ip = offset;
		_top = 0;
		_callTop = 0;
		_jumpBudget = JumpBudget;
		_callBudget = CallBudget;

		Failed = false;

		while ( !Failed )
		{
			if ( _ip >= _codeEnd )
			{
				if ( _callTop == 0 )
					return;

				EndFunction();
				continue;
			}

			if ( --_instructionBudget == 0 )
			{
				Fail();
				return;
			}

			Step( _code[_ip++] );
		}
	}

	private void EndFunction()
	{
		ref var record = ref _calls[_callTop - 1];

		if ( --record.Count > 0 )
		{
			_ip = record.Definition.Start;
			return;
		}

		_code = record.Code;
		_codeStart = record.CodeStart;
		_codeEnd = record.CodeEnd;
		_ip = record.ReturnIp;
		_callTop--;
		_callBudget++;
	}

	private void PushBytes( int count )
	{
		if ( _top + count > _stackLimit || _ip + count > _codeEnd )
		{
			Fail();
			return;
		}

		for ( ; count > 0; count-- )
			_stack[_top++] = _code[_ip++];
	}

	private void PushWords( int count )
	{
		if ( _top + count > _stackLimit || _ip + (count * 2) > _codeEnd )
		{
			Fail();
			return;
		}

		for ( ; count > 0; count-- )
		{
			_stack[_top++] = (short)((_code[_ip] << 8) | _code[_ip + 1]);
			_ip += 2;
		}
	}

	private void Step( int opcode )
	{
		var need = StackNeed( opcode );

		if ( need < 0 || _top < need )
		{
			Fail();
			return;
		}

		if ( opcode >= 0xC0 )
		{
			if ( opcode >= 0xE0 )
				Mirp( opcode );
			else
				Mdrp( opcode );

			return;
		}

		if ( opcode is >= 0xB0 and <= 0xB7 )
		{
			PushBytes( opcode - 0xB0 + 1 );
			return;
		}

		if ( opcode is >= 0xB8 and <= 0xBF )
		{
			PushWords( opcode - 0xB8 + 1 );
			return;
		}

		switch ( opcode )
		{
			case 0x00:
			case 0x01:
				_gs.ProjX = _gs.DualX = _gs.FreeX = opcode == 1 ? 0x4000 : 0;
				_gs.ProjY = _gs.DualY = _gs.FreeY = opcode == 1 ? 0 : 0x4000;
				_gs.PfProj = 0x4000;
				break;

			case 0x02:
			case 0x03:
				_gs.ProjX = _gs.DualX = opcode == 3 ? 0x4000 : 0;
				_gs.ProjY = _gs.DualY = opcode == 3 ? 0 : 0x4000;
				_gs.PfProj = opcode == 3 ? _gs.FreeX : _gs.FreeY;
				CheckPfProj();
				break;

			case 0x04:
			case 0x05:
				_gs.FreeX = opcode == 5 ? 0x4000 : 0;
				_gs.FreeY = opcode == 5 ? 0 : 0x4000;
				_gs.PfProj = opcode == 5 ? _gs.ProjX : _gs.ProjY;
				CheckPfProj();
				break;

			case 0x06:
			case 0x07:
			case 0x08:
			case 0x09:
			{
				var point2 = Pop();
				var point1 = Pop();

				if ( !CheckPoint( _zp2, point2 ) || !CheckPoint( _zp1, point1 ) )
					break;

				Normalize( _zp1.CurX[point1] - _zp2.CurX[point2], _zp1.CurY[point1] - _zp2.CurY[point2], out var x, out var y );

				if ( (opcode & 1) != 0 )
					(x, y) = (-y, x);

				if ( opcode <= 0x07 )
				{
					_gs.ProjX = _gs.DualX = x;
					_gs.ProjY = _gs.DualY = y;
				}
				else
				{
					_gs.FreeX = x;
					_gs.FreeY = y;
				}

				ComputePfProj();
				break;
			}

			case 0x0A:
			case 0x0B:
			{
				var y = (short)Pop();
				var x = (short)Pop();

				if ( opcode == 0x0A )
				{
					_gs.ProjX = _gs.DualX = x;
					_gs.ProjY = _gs.DualY = y;
				}
				else
				{
					_gs.FreeX = x;
					_gs.FreeY = y;
				}

				ComputePfProj();
				break;
			}

			case 0x0C:
				Push( _gs.ProjX );
				Push( _gs.ProjY );
				break;

			case 0x0D:
				Push( _gs.FreeX );
				Push( _gs.FreeY );
				break;

			case 0x0E:
				_gs.FreeX = _gs.ProjX;
				_gs.FreeY = _gs.ProjY;
				_gs.PfProj = 0x4000;
				break;

			case 0x0F:
				Intersect();
				break;

			case 0x10:
				_gs.Rp0 = Pop();
				break;

			case 0x11:
				_gs.Rp1 = Pop();
				break;

			case 0x12:
				_gs.Rp2 = Pop();
				break;

			case 0x13:
			case 0x14:
			case 0x15:
			case 0x16:
			{
				var index = Pop();

				if ( (uint)index > 1 )
				{
					Fail();
					break;
				}

				if ( opcode is 0x13 or 0x16 )
				{
					_gs.Gep0 = index;
					_zp0 = ZoneFor( index );
				}

				if ( opcode is 0x14 or 0x16 )
				{
					_gs.Gep1 = index;
					_zp1 = ZoneFor( index );
				}

				if ( opcode is 0x15 or 0x16 )
				{
					_gs.Gep2 = index;
					_zp2 = ZoneFor( index );
				}

				break;
			}

			case 0x17:
				_gs.Loop = Pop();
				break;

			case 0x18:
				_gs.RoundState = RoundToGrid;
				break;

			case 0x19:
				_gs.RoundState = RoundToHalfGrid;
				break;

			case 0x1A:
				_gs.MinimumDistance = Pop();
				break;

			case 0x1B:
				Skip( false );
				break;

			case 0x1C:
				Jump( Pop() );
				break;

			case 0x1D:
				_gs.ControlValueCutIn = Pop();
				break;

			case 0x1E:
				_gs.SingleWidthCutIn = Pop();
				break;

			case 0x1F:
				_gs.SingleWidthValue = _scale.Apply( (short)Pop() );
				break;

			case 0x20:
			{
				var value = Pop();

				Push( value );
				Push( value );
				break;
			}

			case 0x21:
				Pop();
				break;

			case 0x22:
				_top = 0;
				break;

			case 0x23:
			{
				var a = Pop();
				var b = Pop();

				Push( a );
				Push( b );
				break;
			}

			case 0x24:
				Push( _top );
				break;

			case 0x25:
			{
				var index = _stack[_top - 1];

				if ( index < 0 || index > _top - 1 )
				{
					Fail();
					break;
				}

				_stack[_top - 1] = _stack[_top - 1 - index];
				break;
			}

			case 0x26:
			{
				var index = _stack[_top - 1];

				if ( index < 0 || index > _top - 1 )
				{
					Fail();
					break;
				}

				if ( index == 0 )
					break;

				var value = _stack[_top - 1 - index];

				Array.Copy( _stack, _top - index, _stack, _top - 1 - index, index - 1 );
				_stack[_top - 2] = value;
				_top--;
				break;
			}

			case 0x27:
			{
				var point2 = Pop();
				var point1 = Pop();

				if ( !CheckPoint( _zp1, point2 ) || !CheckPoint( _zp0, point1 ) )
					break;

				var distance = Project( _zp1.CurX[point2] - _zp0.CurX[point1], _zp1.CurY[point2] - _zp0.CurY[point1] );

				MovePoint( _zp0, point1, distance >> 1 );
				MovePoint( _zp1, point2, (distance >> 1) - distance );
				break;
			}

			case 0x28:
			{
				if ( _phase != GlyphProgram )
				{
					Fail();
					break;
				}

				var phantom = GlyphZone.Contours[GlyphZone.ContourCount - 1] + 1;

				Push( GlyphZone.OrgX[phantom + 1] - GlyphZone.OrgX[phantom] );
				break;
			}

			case 0x29:
			{
				var point = Pop();
				var mask = 0xFF;

				if ( !CheckPoint( _zp0, point ) )
					break;

				if ( _gs.FreeX != 0 )
					mask &= ~Zone.TouchX;

				if ( _gs.FreeY != 0 )
					mask &= ~Zone.TouchY;

				_zp0.Tags[point] &= (byte)mask;
				break;
			}

			case 0x2A:
			case 0x2B:
			{
				var function = Pop();

				if ( function < 0 || function >= _functionLimit )
				{
					Fail();
					break;
				}

				Call( _functions[function], opcode == 0x2A ? Pop() : 1 );
				break;
			}

			case 0x2C:
			case 0x89:
				Define( opcode );
				break;

			case 0x2D:
				Fail();
				break;

			case 0x2E:
			case 0x2F:
			{
				var point = Pop();
				var distance = 0;

				if ( !CheckPoint( _zp0, point ) )
					break;

				if ( (opcode & 1) != 0 )
				{
					var current = Project( _zp0.CurX[point], _zp0.CurY[point] );

					distance = Round( current ) - current;
				}

				MovePoint( _zp0, point, distance );
				_gs.Rp0 = _gs.Rp1 = point;
				break;
			}

			case 0x30:
			case 0x31:
				InterpolateUntouched( opcode == 0x31 );
				break;

			case 0x32:
			case 0x33:
				ShiftPoints( opcode );
				break;

			case 0x34:
			case 0x35:
				ShiftContour( opcode );
				break;

			case 0x36:
			case 0x37:
				ShiftZone( opcode );
				break;

			case 0x38:
			{
				var distance = Pop();
				var dx = VectorMul( distance, _gs.FreeX );
				var dy = VectorMul( distance, _gs.FreeY );

				for ( ; _gs.Loop > 0; _gs.Loop-- )
				{
					var point = Pop();

					if ( !CheckPoint( _zp2, point ) )
						return;

					MoveBy( _zp2, point, dx, dy, true );
				}

				_gs.Loop = 1;
				break;
			}

			case 0x39:
				InterpolatePoints();
				break;

			case 0x3A:
			case 0x3B:
			{
				var distance = Pop();
				var point = Pop();

				if ( !CheckPoint( _zp1, point ) || !CheckPoint( _zp0, _gs.Rp0 ) )
					break;

				if ( _gs.Gep1 == 0 )
				{
					_zp1.OrgX[point] = _zp0.OrgX[_gs.Rp0] + VectorMul( distance, _gs.ProjX );
					_zp1.OrgY[point] = _zp0.OrgY[_gs.Rp0] + VectorMul( distance, _gs.ProjY );
					_zp1.CurX[point] = _zp1.OrgX[point];
					_zp1.CurY[point] = _zp1.OrgY[point];
				}

				var current = Project( _zp1.CurX[point] - _zp0.CurX[_gs.Rp0], _zp1.CurY[point] - _zp0.CurY[_gs.Rp0] );

				MovePoint( _zp1, point, distance - current );

				_gs.Rp1 = _gs.Rp0;
				_gs.Rp2 = point;

				if ( (opcode & 1) != 0 )
					_gs.Rp0 = point;

				break;
			}

			case 0x3C:
				if ( !CheckPoint( _zp0, _gs.Rp0 ) )
					break;

				for ( ; _gs.Loop > 0; _gs.Loop-- )
				{
					var point = Pop();

					if ( !CheckPoint( _zp1, point ) )
						return;

					var distance = Project( _zp1.CurX[point] - _zp0.CurX[_gs.Rp0], _zp1.CurY[point] - _zp0.CurY[_gs.Rp0] );

					MovePoint( _zp1, point, -distance );
				}

				_gs.Loop = 1;
				break;

			case 0x3D:
				_gs.RoundState = RoundToDoubleGrid;
				break;

			case 0x3E:
			case 0x3F:
			{
				var entry = Pop();
				var point = Pop();

				if ( !CheckCvt( entry ) || !CheckPoint( _zp0, point ) )
					break;

				var distance = _cvt[entry];

				if ( _gs.Gep0 == 0 )
				{
					_zp0.OrgX[point] = VectorMul( distance, _gs.ProjX );
					_zp0.OrgY[point] = VectorMul( distance, _gs.ProjY );
					_zp0.CurX[point] = _zp0.OrgX[point];
					_zp0.CurY[point] = _zp0.OrgY[point];
				}

				var original = Project( _zp0.CurX[point], _zp0.CurY[point] );

				if ( (opcode & 1) != 0 )
				{
					if ( Math.Abs( distance - original ) > _gs.ControlValueCutIn )
						distance = original;

					distance = Round( distance );
				}

				MovePoint( _zp0, point, distance - original );
				_gs.Rp0 = _gs.Rp1 = point;
				break;
			}

			case 0x40:
			case 0x41:
				if ( _ip >= _codeEnd )
				{
					Fail();
					break;
				}

				if ( opcode == 0x40 )
					PushBytes( _code[_ip++] );
				else
					PushWords( _code[_ip++] );

				break;

			case 0x42:
			{
				var value = Pop();
				var index = Pop();

				if ( index < 0 || index >= _storageLimit )
				{
					Fail();
					break;
				}

				_storage[index] = value;
				break;
			}

			case 0x43:
			{
				var index = Pop();

				if ( index < 0 || index >= _storageLimit )
				{
					Fail();
					break;
				}

				Push( _storage[index] );
				break;
			}

			case 0x44:
			{
				var value = Pop();
				var index = Pop();

				if ( CheckCvt( index ) )
					_cvt[index] = value;

				break;
			}

			case 0x45:
			{
				var index = Pop();

				if ( index < 0 || (index >= _cvt.Length && index >= 0x100) )
				{
					Fail();
					break;
				}

				Push( index < _cvt.Length ? _cvt[index] : 0 );
				break;
			}

			case 0x46:
			case 0x47:
			{
				var point = Pop();

				if ( !CheckPoint( _zp2, point ) )
					break;

				Push( opcode == 0x46
					? Project( _zp2.CurX[point], _zp2.CurY[point] )
					: DualProject( _zp2.OrgX[point], _zp2.OrgY[point] ) );

				break;
			}

			case 0x48:
			{
				var value = Pop();
				var point = Pop();

				if ( !CheckPoint( _zp2, point ) )
					break;

				var current = Project( _zp2.CurX[point], _zp2.CurY[point] );

				MovePoint( _zp2, point, value - current );

				if ( _zp2 == _twilight )
				{
					_zp2.OrgX[point] = _zp2.CurX[point];
					_zp2.OrgY[point] = _zp2.CurY[point];
				}

				break;
			}

			case 0x49:
			case 0x4A:
			{
				var point1 = Pop();
				var point2 = Pop();
				int distance;

				if ( !CheckPoint( _zp1, point1 ) || !CheckPoint( _zp0, point2 ) )
					break;

				if ( (opcode & 1) != 0 )
				{
					distance = Project( _zp0.CurX[point2] - _zp1.CurX[point1], _zp0.CurY[point2] - _zp1.CurY[point1] );
				}
				else if ( _gs.Gep0 == 0 || _gs.Gep1 == 0 )
				{
					distance = DualProject( _zp0.OrgX[point2] - _zp1.OrgX[point1], _zp0.OrgY[point2] - _zp1.OrgY[point1] );
				}
				else
				{
					distance = ScaleOriginal( DualProject( _zp0.OrusX[point2] - _zp1.OrusX[point1], _zp0.OrusY[point2] - _zp1.OrusY[point1] ) );
				}

				Push( distance );
				break;
			}

			case 0x4B:
				Push( _pixelsPerEm );
				break;

			case 0x4C:
				Push( _pointSize );
				break;

			case 0x4D:
				_gs.AutoFlip = true;
				break;

			case 0x4E:
				_gs.AutoFlip = false;
				break;

			case 0x4F:
				Pop();
				break;

			case 0x50:
			{
				var b = Pop();
				var a = Pop();

				Push( a < b ? 1 : 0 );
				break;
			}

			case 0x51:
			{
				var b = Pop();
				var a = Pop();

				Push( a <= b ? 1 : 0 );
				break;
			}

			case 0x52:
			{
				var b = Pop();
				var a = Pop();

				Push( a > b ? 1 : 0 );
				break;
			}

			case 0x53:
			{
				var b = Pop();
				var a = Pop();

				Push( a >= b ? 1 : 0 );
				break;
			}

			case 0x54:
			{
				var b = Pop();
				var a = Pop();

				Push( a == b ? 1 : 0 );
				break;
			}

			case 0x55:
			{
				var b = Pop();
				var a = Pop();

				Push( a != b ? 1 : 0 );
				break;
			}

			case 0x56:
				Push( (RoundGrid( Pop() ) >> 6) & 1 );
				break;

			case 0x57:
				Push( ((RoundGrid( Pop() ) >> 6) - 1) & 1 );
				break;

			case 0x58:
				if ( Pop() == 0 )
					Skip( true );

				break;

			case 0x59:
				break;

			case 0x5A:
			{
				var b = Pop();
				var a = Pop();

				Push( a != 0 && b != 0 ? 1 : 0 );
				break;
			}

			case 0x5B:
			{
				var b = Pop();
				var a = Pop();

				Push( a != 0 || b != 0 ? 1 : 0 );
				break;
			}

			case 0x5C:
				Push( Pop() == 0 ? 1 : 0 );
				break;

			case 0x5D:
			case 0x71:
			case 0x72:
				DeltaPoint( opcode );
				break;

			case 0x5E:
				_gs.DeltaBase = Pop();
				break;

			case 0x5F:
				_gs.DeltaShift = Pop();
				break;

			case 0x60:
			{
				var b = Pop();
				var a = Pop();

				Push( a + b );
				break;
			}

			case 0x61:
			{
				var b = Pop();
				var a = Pop();

				Push( a - b );
				break;
			}

			case 0x62:
			{
				var b = Pop();
				var a = Pop();

				if ( b == 0 )
				{
					Fail();
					break;
				}

				Push( (int)(((long)a << 6) / b) );
				break;
			}

			case 0x63:
			{
				var b = Pop();
				var a = Pop();

				Push( Fixed.Mul26Dot6( a, b ) );
				break;
			}

			case 0x64:
				Push( Math.Abs( Pop() ) );
				break;

			case 0x65:
				Push( -Pop() );
				break;

			case 0x66:
				Push( Fixed.PixFloor( Pop() ) );
				break;

			case 0x67:
				Push( Fixed.PixCeil( Pop() ) );
				break;

			case 0x68:
			case 0x69:
			case 0x6A:
			case 0x6B:
				Push( Round( Pop() ) );
				break;

			case 0x6C:
			case 0x6D:
			case 0x6E:
			case 0x6F:
				break;

			case 0x70:
			{
				var value = Pop();
				var index = Pop();

				if ( CheckCvt( index ) )
					_cvt[index] = _scale.Apply( value );

				break;
			}

			case 0x73:
			case 0x74:
			case 0x75:
				DeltaCvt( opcode );
				break;

			case 0x76:
				SetSuperRound( Pop() );
				_gs.RoundState = RoundSuper;
				break;

			case 0x77:
				SetSuperRound45( Pop() );
				_gs.RoundState = RoundSuper45;
				break;

			case 0x78:
			{
				var condition = Pop();
				var offset = Pop();

				if ( condition != 0 )
					Jump( offset );

				break;
			}

			case 0x79:
			{
				var condition = Pop();
				var offset = Pop();

				if ( condition == 0 )
					Jump( offset );

				break;
			}

			case 0x7A:
				_gs.RoundState = RoundOff;
				break;

			case 0x7C:
				_gs.RoundState = RoundUpToGrid;
				break;

			case 0x7D:
				_gs.RoundState = RoundDownToGrid;
				break;

			case 0x7E:
			case 0x7F:
				Pop();
				break;

			case 0x80:
				for ( ; _gs.Loop > 0; _gs.Loop-- )
				{
					var point = Pop();

					if ( !CheckPoint( _zp0, point ) )
						return;

					_zp0.Tags[point] ^= Zone.OnCurve;
				}

				_gs.Loop = 1;
				break;

			case 0x81:
			case 0x82:
			{
				var high = Pop();
				var low = Pop();

				if ( !CheckPoint( _zp0, high ) || !CheckPoint( _zp0, low ) )
					break;

				for ( var point = low; point <= high; point++ )
				{
					if ( opcode == 0x81 )
						_zp0.Tags[point] |= Zone.OnCurve;
					else
						_zp0.Tags[point] &= unchecked((byte)~Zone.OnCurve);
				}

				break;
			}

			case 0x85:
				_gs.ScanState = (_gs.ScanState & unchecked((int)0xFFFF0000)) | Pop();
				break;

			case 0x86:
			case 0x87:
			{
				var point2 = Pop();
				var point1 = Pop();

				if ( !CheckPoint( _zp2, point2 ) || !CheckPoint( _zp1, point1 ) )
					break;

				Normalize( _zp1.CurX[point1] - _zp2.CurX[point2], _zp1.CurY[point1] - _zp2.CurY[point2], out _gs.ProjX, out _gs.ProjY );
				Normalize( _zp1.OrgX[point1] - _zp2.OrgX[point2], _zp1.OrgY[point1] - _zp2.OrgY[point2], out _gs.DualX, out _gs.DualY );

				if ( (opcode & 1) != 0 )
				{
					(_gs.ProjX, _gs.ProjY) = (-_gs.ProjY, _gs.ProjX);
					(_gs.DualX, _gs.DualY) = (-_gs.DualY, _gs.DualX);
				}

				ComputePfProj();
				break;
			}

			case 0x88:
			{
				var selector = Pop();

				var result = (selector & 1) != 0 ? RasterizerVersion : 0;

				if ( (selector & 8) != 0 )
					result |= 0x400;

				if ( (selector & 0x10) != 0 )
					result |= 0x800;

				if ( (selector & 0x20) != 0 && Grayscale )
					result |= 0x1000;

				Push( result );
				break;
			}

			case 0x8A:
			{
				var a = Pop();
				var b = Pop();
				var c = Pop();

				Push( b );
				Push( a );
				Push( c );
				break;
			}

			case 0x8B:
			{
				var b = Pop();
				var a = Pop();

				Push( Math.Max( a, b ) );
				break;
			}

			case 0x8C:
			{
				var b = Pop();
				var a = Pop();

				Push( Math.Min( a, b ) );
				break;
			}

			case 0x8D:
				_gs.ScanState = (Pop() << 16) | (_gs.ScanState & 0xFFFF);
				break;

			case 0x8E:
			{
				var selector = Pop();
				var value = Pop();

				if ( _phase != ControlProgram )
					break;

				if ( selector is >= 1 and <= 3 )
					_gs.InstructControl &= ~(1 << (selector - 1));

				_gs.InstructControl |= value;
				break;
			}

			default:
				Call( _instructions[opcode], 1 );
				break;
		}
	}

	private void MoveBy( Zone zone, int point, int dx, int dy, bool touch )
	{
		if ( _gs.FreeX != 0 )
		{
			zone.CurX[point] += dx;

			if ( touch )
				zone.Tags[point] |= Zone.TouchX;
		}

		if ( _gs.FreeY != 0 )
		{
			zone.CurY[point] += dy;

			if ( touch )
				zone.Tags[point] |= Zone.TouchY;
		}
	}

	private bool ShiftDisplacement( int opcode, out Zone zone, out int reference, out int dx, out int dy )
	{
		if ( (opcode & 1) != 0 )
		{
			zone = _zp0;
			reference = _gs.Rp1;
		}
		else
		{
			zone = _zp1;
			reference = _gs.Rp2;
		}

		dx = 0;
		dy = 0;

		if ( !CheckPoint( zone, reference ) )
			return false;

		var distance = Project( zone.CurX[reference] - zone.OrgX[reference], zone.CurY[reference] - zone.OrgY[reference] );
		var dot = FreeDotProj();

		if ( dot == 0x4000 )
		{
			dx = VectorMul( distance, _gs.FreeX );
			dy = VectorMul( distance, _gs.FreeY );
			return true;
		}

		dx = _gs.FreeX != 0 ? Fixed.RoundedDivide( (long)_gs.FreeX * distance, dot ) : 0;
		dy = _gs.FreeY != 0 ? Fixed.RoundedDivide( (long)_gs.FreeY * distance, dot ) : 0;

		return true;
	}

	private void ShiftPoints( int opcode )
	{
		if ( !ShiftDisplacement( opcode, out _, out _, out var dx, out var dy ) )
			return;

		for ( ; _gs.Loop > 0; _gs.Loop-- )
		{
			var point = Pop();

			if ( !CheckPoint( _zp2, point ) )
				return;

			MoveBy( _zp2, point, dx, dy, true );
		}

		_gs.Loop = 1;
	}

	private void ShiftContour( int opcode )
	{
		if ( !ShiftDisplacement( opcode, out var zone, out var reference, out var dx, out var dy ) )
			return;

		var contour = Pop();

		if ( !CheckProgram() )
			return;

		if ( contour < 0 || contour >= _zp2.ContourCount )
		{
			Fail();
			return;
		}

		var start = contour == 0 ? 0 : _zp2.Contours[contour - 1] + 1;
		var end = _zp2.Contours[contour];

		for ( var point = start; point <= end; point++ )
		{
			if ( zone != _zp2 || point != reference )
				MoveBy( _zp2, point, dx, dy, true );
		}
	}

	private void ShiftZone( int opcode )
	{
		if ( !ShiftDisplacement( opcode, out var zone, out var reference, out var dx, out var dy ) )
			return;

		var index = Pop();

		if ( (uint)index > 1 )
		{
			Fail();
			return;
		}

		var target = ZoneFor( index );

		if ( _phase != GlyphProgram && !(_phase == FontProgram && target == _twilight) )
		{
			Fail();
			return;
		}

		int last;

		if ( target == _twilight )
			last = target.PointCount - 1;
		else if ( target.ContourCount > 0 )
			last = target.Contours[target.ContourCount - 1];
		else
			return;

		for ( var point = 0; point <= last; point++ )
		{
			if ( zone != target || point != reference )
				MoveBy( target, point, dx, dy, false );
		}
	}

	private void Intersect()
	{
		var b1 = Pop();
		var b0 = Pop();
		var a1 = Pop();
		var a0 = Pop();
		var point = Pop();

		if ( !CheckPoint( _zp0, b1 ) || !CheckPoint( _zp0, b0 ) || !CheckPoint( _zp1, a1 ) || !CheckPoint( _zp1, a0 ) || !CheckPoint( _zp2, point ) )
			return;

		var bx = _zp0.CurX[b0];
		var by = _zp0.CurY[b0];
		var dbx = _zp0.CurX[b1] - bx;
		var dby = _zp0.CurY[b1] - by;
		var ax = _zp1.CurX[a0];
		var ay = _zp1.CurY[a0];
		var dax = _zp1.CurX[a1] - ax;
		var day = _zp1.CurY[a1] - ay;

		_zp2.Tags[point] |= Zone.TouchX | Zone.TouchY;

		int numerator;
		int denominator;

		if ( dby == 0 )
		{
			if ( dax == 0 )
			{
				_zp2.CurX[point] = ax;
				_zp2.CurY[point] = by;
				return;
			}

			numerator = ay - by;
			denominator = -day;
		}
		else if ( dbx == 0 )
		{
			if ( day == 0 )
			{
				_zp2.CurX[point] = bx;
				_zp2.CurY[point] = ay;
				return;
			}

			numerator = ax - bx;
			denominator = -dax;
		}
		else if ( Math.Abs( dbx ) < Math.Abs( dby ) )
		{
			numerator = (bx - ax) + Fixed.RoundedDivide( (long)(ay - by) * dbx, dby );
			denominator = dax - Fixed.RoundedDivide( (long)day * dbx, dby );
		}
		else
		{
			numerator = (ay - by) - Fixed.RoundedDivide( (long)(ax - bx) * dby, dbx );
			denominator = Fixed.RoundedDivide( (long)dax * dby, dbx ) - day;
		}

		if ( denominator == 0 )
		{
			_zp2.CurY[point] = ((day >> 1) + by + (dby >> 1) + ay) >> 1;
			_zp2.CurX[point] = ((dax >> 1) + bx + (dbx >> 1) + ax) >> 1;
			return;
		}

		_zp2.CurY[point] = Fixed.RoundedDivide( (long)day * numerator, denominator ) + ay;
		_zp2.CurX[point] = Fixed.RoundedDivide( (long)dax * numerator, denominator ) + ax;
	}

	private void InterpolatePoints()
	{
		var twilight = _gs.Gep0 == 0 || _gs.Gep1 == 0 || _gs.Gep2 == 0;

		if ( !CheckPoint( _zp0, _gs.Rp1 ) || !CheckPoint( _zp1, _gs.Rp2 ) )
			return;

		int baseX;
		int baseY;

		if ( twilight )
		{
			baseX = _zp0.OrgX[_gs.Rp1];
			baseY = _zp0.OrgY[_gs.Rp1];
		}
		else
		{
			baseX = _zp0.OrusX[_gs.Rp1];
			baseY = _zp0.OrusY[_gs.Rp1];
		}

		var currentBaseX = _zp0.CurX[_gs.Rp1];
		var currentBaseY = _zp0.CurY[_gs.Rp1];

		var oldRange = twilight
			? DualProject( _zp1.OrgX[_gs.Rp2] - baseX, _zp1.OrgY[_gs.Rp2] - baseY )
			: DualProject( _zp1.OrusX[_gs.Rp2] - baseX, _zp1.OrusY[_gs.Rp2] - baseY );

		var currentRange = Project( _zp1.CurX[_gs.Rp2] - currentBaseX, _zp1.CurY[_gs.Rp2] - currentBaseY );

		for ( ; _gs.Loop > 0; _gs.Loop-- )
		{
			var point = Pop();

			if ( !CheckPoint( _zp2, point ) )
				return;

			var originalDistance = twilight
				? Project( _zp2.OrgX[point] - baseX, _zp2.OrgY[point] - baseY )
				: Project( _zp2.OrusX[point] - baseX, _zp2.OrusY[point] - baseY );

			var currentDistance = Project( _zp2.CurX[point] - currentBaseX, _zp2.CurY[point] - currentBaseY );
			var newDistance = oldRange != 0 ? Fixed.RoundedDivide( (long)originalDistance * currentRange, oldRange ) : originalDistance;

			MovePoint( _zp2, point, newDistance - currentDistance );
		}

		_gs.Loop = 1;
	}

	private void Mdrp( int opcode )
	{
		var point = Pop();
		int originalDistance;

		if ( !CheckPoint( _zp0, _gs.Rp0 ) || !CheckPoint( _zp1, point ) )
			return;

		if ( _gs.Gep0 == 0 || _gs.Gep1 == 0 )
		{
			originalDistance = DualProject( _zp1.OrgX[point] - _zp0.OrgX[_gs.Rp0], _zp1.OrgY[point] - _zp0.OrgY[_gs.Rp0] );
		}
		else
		{
			originalDistance = ScaleOriginal( DualProject( _zp1.OrusX[point] - _zp0.OrusX[_gs.Rp0], _zp1.OrusY[point] - _zp0.OrusY[_gs.Rp0] ) );
		}

		originalDistance = CheckSingleWidth( originalDistance );

		var distance = (opcode & 4) != 0 ? Round( originalDistance ) : originalDistance;

		if ( (opcode & 8) != 0 )
		{
			if ( originalDistance >= 0 )
			{
				if ( distance < _gs.MinimumDistance )
					distance = _gs.MinimumDistance;
			}
			else if ( distance > -_gs.MinimumDistance )
			{
				distance = -_gs.MinimumDistance;
			}
		}

		var current = Project( _zp1.CurX[point] - _zp0.CurX[_gs.Rp0], _zp1.CurY[point] - _zp0.CurY[_gs.Rp0] );

		MovePoint( _zp1, point, distance - current );

		_gs.Rp1 = _gs.Rp0;
		_gs.Rp2 = point;

		if ( (opcode & 16) != 0 )
			_gs.Rp0 = point;
	}

	private void Mirp( int opcode )
	{
		var entry = Pop();
		var point = Pop();

		if ( !CheckCvt( entry ) || !CheckPoint( _zp0, _gs.Rp0 ) || !CheckPoint( _zp1, point ) )
			return;

		var cvtDistance = CheckSingleWidth( _cvt[entry] );

		if ( _gs.Gep1 == 0 )
		{
			_zp1.OrgX[point] = _zp0.OrgX[_gs.Rp0] + VectorMul( cvtDistance, _gs.ProjX );
			_zp1.OrgY[point] = _zp0.OrgY[_gs.Rp0] + VectorMul( cvtDistance, _gs.ProjY );
			_zp1.CurX[point] = _zp1.OrgX[point];
			_zp1.CurY[point] = _zp1.OrgY[point];
		}

		var originalDistance = DualProject( _zp1.OrgX[point] - _zp0.OrgX[_gs.Rp0], _zp1.OrgY[point] - _zp0.OrgY[_gs.Rp0] );
		var currentDistance = Project( _zp1.CurX[point] - _zp0.CurX[_gs.Rp0], _zp1.CurY[point] - _zp0.CurY[_gs.Rp0] );

		if ( _gs.AutoFlip && (originalDistance ^ cvtDistance) < 0 )
			cvtDistance = -cvtDistance;

		int distance;

		if ( (opcode & 4) != 0 )
		{
			if ( cvtDistance - originalDistance > _gs.ControlValueCutIn || cvtDistance - originalDistance < -_gs.ControlValueCutIn )
				cvtDistance = originalDistance;

			distance = Round( cvtDistance );
		}
		else
		{
			distance = cvtDistance;
		}

		if ( (opcode & 8) != 0 )
		{
			if ( originalDistance >= 0 )
			{
				if ( distance < _gs.MinimumDistance )
					distance = _gs.MinimumDistance;
			}
			else if ( distance > -_gs.MinimumDistance )
			{
				distance = -_gs.MinimumDistance;
			}
		}

		MovePoint( _zp1, point, distance - currentDistance );

		_gs.Rp1 = _gs.Rp0;

		if ( (opcode & 16) != 0 )
			_gs.Rp0 = point;

		_gs.Rp2 = point;
	}

	private void DeltaPoint( int opcode )
	{
		Delta( _gs.DeltaBase + (opcode == 0x5D ? 0 : opcode == 0x71 ? 16 : 32), true );
	}

	private void DeltaCvt( int opcode )
	{
		Delta( _gs.DeltaBase + ((opcode - 0x73) * 16), false );
	}

	private void Delta( int deltaBase, bool points )
	{
		var count = Pop() * 2;

		if ( count < 0 || count > _top )
		{
			Fail();
			return;
		}

		_top -= count;

		var first = _top;
		var range = _pixelsPerEm - deltaBase;

		if ( (uint)range > 0xF )
			return;

		var target = range * 0x10;
		var position = 0;
		var step = (count >> 1) & ~1;

		while ( step > 2 )
		{
			if ( target > (_stack[first + position + step] & ~0xF) )
				position += step;

			step = (step >> 1) & ~1;
		}

		for ( ; position < count; position += 2 )
		{
			var argument = _stack[first + position];
			var key = argument & ~0xF;

			if ( key != target )
			{
				if ( target <= key )
					return;

				continue;
			}

			var index = _stack[first + position + 1];
			var magnitude = argument & 0xF;
			var amount = ((magnitude - (magnitude < 8 ? 8 : 7)) * 0x40) >> _gs.DeltaShift;

			if ( points )
			{
				if ( !CheckPoint( _zp0, index ) )
					return;

				MovePoint( _zp0, index, amount );
			}
			else
			{
				if ( !CheckCvt( index ) )
					return;

				_cvt[index] += amount;
			}
		}
	}

	private void InterpolateUntouched( bool horizontal )
	{
		if ( !CheckProgram() )
			return;

		var zone = _zp2;
		var current = horizontal ? zone.CurX : zone.CurY;
		var original = horizontal ? zone.OrgX : zone.OrgY;
		var unscaled = horizontal ? zone.OrusX : zone.OrusY;
		var mask = horizontal ? Zone.TouchX : Zone.TouchY;

		var contourStart = 0;

		for ( var contour = 0; contour < zone.ContourCount; contour++ )
		{
			var contourEnd = zone.Contours[contour];
			var first = contourStart;

			while ( first <= contourEnd && (zone.Tags[first] & mask) == 0 )
				first++;

			if ( first <= contourEnd )
			{
				var touched = first;

				do
				{
					var next = touched == contourEnd ? contourStart : touched + 1;

					while ( (zone.Tags[next] & mask) != 0 && next != first )
					{
						touched = next;
						next = touched == contourEnd ? contourStart : touched + 1;
					}

					if ( next == first )
						break;

					var end = next;

					while ( (zone.Tags[end] & mask) == 0 )
						end = end == contourEnd ? contourStart : end + 1;

					InterpolateRange( current, original, unscaled, next, end, touched, end, contourStart, contourEnd );

					touched = end;
				}
				while ( touched != first );
			}

			contourStart = contourEnd + 1;
		}
	}

	private static void InterpolateRange( int[] current, int[] original, int[] unscaled, int first, int end, int reference1, int reference2, int contourStart, int contourEnd )
	{
		var low = reference2;
		var high = reference1;

		if ( unscaled[reference1] < unscaled[reference2] )
		{
			low = reference1;
			high = reference2;
		}

		var range = unscaled[high] - unscaled[low];
		var unscaledLow = unscaled[low];
		var currentLow = current[low];
		var originalLow = original[low];
		var deltaLow = currentLow - originalLow;

		if ( range == 0 )
		{
			for ( var point = first; point != end; point = point == contourEnd ? contourStart : point + 1 )
				current[point] += deltaLow;

			return;
		}

		var originalHigh = original[high];
		var deltaHigh = current[high] - originalHigh;
		var currentRange = current[high] - currentLow;

		if ( range < 0x8000 && currentRange < 0x8000 )
		{
			for ( var point = first; point != end; point = point == contourEnd ? contourStart : point + 1 )
			{
				var value = original[point];

				if ( originalLow < value )
					value = value < originalHigh ? ((((unscaled[point] - unscaledLow) * currentRange) + (range >> 1)) / range) + currentLow : value + deltaHigh;
				else if ( originalHigh <= value )
					value += deltaHigh;
				else
					value += deltaLow;

				current[point] = value;
			}

			return;
		}

		var scale = Fixed.FixDiv( currentRange, range );

		for ( var point = first; point != end; point = point == contourEnd ? contourStart : point + 1 )
		{
			var value = original[point];

			if ( originalLow < value )
				value = value < originalHigh ? Fixed.FixMul( unscaled[point] - unscaledLow, scale ) + currentLow : value + deltaHigh;
			else
				value += deltaLow;

			current[point] = value;
		}
	}
}
