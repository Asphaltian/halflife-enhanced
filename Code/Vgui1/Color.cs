namespace GoldSrc.Vgui1;

public struct Color
{
	private byte _r;
	private byte _g;
	private byte _b;
	private byte _a;
	private SchemeColor _schemeColor;

	public Color( int r, int g, int b, int a )
	{
		SetColor( r, g, b, a );
	}

	public Color( SchemeColor sc )
	{
		SetColor( sc );
	}

	public void SetColor( int r, int g, int b, int a )
	{
		_r = (byte)r;
		_g = (byte)g;
		_b = (byte)b;
		_a = (byte)a;
		_schemeColor = SchemeColor.User;
	}

	public void SetColor( SchemeColor sc )
	{
		_schemeColor = sc;
	}

	public readonly void GetColor( out int r, out int g, out int b, out int a )
	{
		if ( _schemeColor == SchemeColor.User )
		{
			r = _r;
			g = _g;
			b = _b;
			a = _a;
			return;
		}

		App.GetInstance().GetScheme().GetColor( _schemeColor, out r, out g, out b, out a );
	}

	public readonly void GetColor( out SchemeColor sc )
	{
		sc = _schemeColor;
	}

	public readonly int this[int index]
	{
		get
		{
			GetColor( out var r, out var g, out var b, out var a );

			return index switch
			{
				0 => r,
				1 => g,
				2 => b,
				_ => a
			};
		}
	}
}
