namespace GoldSrc.Vgui1;

public enum SchemeColor
{
	User,
	Black,
	White,
	Primary1,
	Primary2,
	Primary3,
	Secondary1,
	Secondary2,
	Secondary3,
	Last
}

public enum SchemeFont
{
	User,
	Primary1,
	Primary2,
	Primary3,
	Secondary1,
	Last
}

public enum SchemeCursor
{
	User,
	None,
	Arrow,
	IBeam,
	Hourglass,
	Crosshair,
	Up,
	SizeNwse,
	SizeNesw,
	SizeWe,
	SizeNs,
	SizeAll,
	No,
	Hand,
	Last
}

public class Scheme
{
	private readonly int[,] _color = new int[(int)SchemeColor.Last, 4];
	private readonly Font[] _font = new Font[(int)SchemeFont.Last];
	private readonly Cursor[] _cursor = new Cursor[(int)SchemeCursor.Last];

	public Scheme()
	{
		SetColor( SchemeColor.Black, 0, 0, 0, 0 );
		SetColor( SchemeColor.White, 255, 255, 255, 0 );
		SetColor( SchemeColor.Primary1, 102, 102, 153, 0 );
		SetColor( SchemeColor.Primary2, 153, 153, 204, 0 );
		SetColor( SchemeColor.Primary3, 204, 204, 255, 0 );
		SetColor( SchemeColor.Secondary1, 102, 102, 102, 0 );
		SetColor( SchemeColor.Secondary2, 153, 153, 153, 0 );
		SetColor( SchemeColor.Secondary3, 204, 204, 204, 0 );
		SetColor( SchemeColor.User, 0, 0, 0, 0 );

		SetFont( SchemeFont.Primary1, new Font( "Arial", null, 20, 0, 0f, 400, false, false, false, false ) );
		SetFont( SchemeFont.Primary2, new Font( "FixedSys", null, 18, 0, 0f, 400, false, false, false, false ) );
		SetFont( SchemeFont.Primary3, new Font( "Arial", null, 12, 0, 0f, 400, false, false, false, false ) );
		SetFont( SchemeFont.Secondary1, new Font( "Marlett", null, 16, 0, 0f, 0, false, false, false, true ) );

		SetCursor( SchemeCursor.None, new Cursor( DefaultCursor.None ) );
		SetCursor( SchemeCursor.Arrow, new Cursor( DefaultCursor.Arrow ) );
		SetCursor( SchemeCursor.IBeam, new Cursor( DefaultCursor.IBeam ) );
		SetCursor( SchemeCursor.Hourglass, new Cursor( DefaultCursor.Hourglass ) );
		SetCursor( SchemeCursor.Crosshair, new Cursor( DefaultCursor.Crosshair ) );
		SetCursor( SchemeCursor.Up, new Cursor( DefaultCursor.Up ) );
		SetCursor( SchemeCursor.SizeNwse, new Cursor( DefaultCursor.SizeNwse ) );
		SetCursor( SchemeCursor.SizeNesw, new Cursor( DefaultCursor.SizeNesw ) );
		SetCursor( SchemeCursor.SizeWe, new Cursor( DefaultCursor.SizeWe ) );
		SetCursor( SchemeCursor.SizeNs, new Cursor( DefaultCursor.SizeNs ) );
		SetCursor( SchemeCursor.SizeAll, new Cursor( DefaultCursor.SizeAll ) );
		SetCursor( SchemeCursor.No, new Cursor( DefaultCursor.No ) );
		SetCursor( SchemeCursor.Hand, new Cursor( DefaultCursor.Hand ) );
	}

	public virtual void SetColor( SchemeColor sc, int r, int g, int b, int a )
	{
		_color[(int)sc, 0] = r;
		_color[(int)sc, 1] = g;
		_color[(int)sc, 2] = b;
		_color[(int)sc, 3] = a;
	}

	public virtual void GetColor( SchemeColor sc, out int r, out int g, out int b, out int a )
	{
		r = _color[(int)sc, 0];
		g = _color[(int)sc, 1];
		b = _color[(int)sc, 2];
		a = _color[(int)sc, 3];
	}

	public virtual void SetFont( SchemeFont sf, Font font )
	{
		if ( font is not null )
			_font[(int)sf] = font;
	}

	public virtual Font GetFont( SchemeFont sf ) => _font[(int)sf];

	public virtual void SetCursor( SchemeCursor sc, Cursor cursor )
	{
		if ( cursor is not null )
			_cursor[(int)sc] = cursor;
	}

	public virtual Cursor GetCursor( SchemeCursor sc ) => _cursor[(int)sc];
}
