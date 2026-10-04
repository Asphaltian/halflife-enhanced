namespace GoldSrc.Vgui1;

public enum DefaultCursor
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

public class Cursor
{
	private sealed class DefaultBitmap : Bitmap
	{
		private const int Size = 16;
		private const int Center = 8;

		private readonly int _hotspotX;
		private readonly int _hotspotY;

		public DefaultBitmap( DefaultCursor dc )
		{
			if ( dc == DefaultCursor.None )
				return;

			_hotspotX = Center;
			_hotspotY = Center;

			SetSize( Size, Size );

			for ( var y = 0; y < Size; y++ )
			{
				for ( var x = 0; x < Size; x++ )
					SetRgba( x, y, 0, 0, 0, 0 );
			}

			for ( var x = 0; x < Size; x++ )
				SetRgba( x, Center, 0, 255, 0, 255 );

			for ( var y = 0; y < Size; y++ )
				SetRgba( Center, y, 0, 0, 255, 255 );
		}

		public void GetHotspot( out int x, out int y )
		{
			x = _hotspotX;
			y = _hotspotY;
		}
	}

	private int _hotspotX;
	private int _hotspotY;
	private Bitmap _bitmap;
	private readonly DefaultCursor _dc;

	public Cursor( DefaultCursor dc )
	{
		_dc = dc;

		var bitmap = new DefaultBitmap( dc );

		bitmap.GetHotspot( out var x, out var y );

		PrivateInit( bitmap, x, y );
	}

	public Cursor( Bitmap bitmap, int hotspotX, int hotspotY )
	{
		_dc = DefaultCursor.User;

		PrivateInit( bitmap, hotspotX, hotspotY );
	}

	public virtual void GetHotspot( out int x, out int y )
	{
		x = _hotspotX;
		y = _hotspotY;
	}

	private void PrivateInit( Bitmap bitmap, int hotspotX, int hotspotY )
	{
		_bitmap = bitmap;
		_hotspotX = hotspotX;
		_hotspotY = hotspotY;
	}

	public virtual Bitmap GetBitmap() => _bitmap;

	public virtual DefaultCursor GetDefaultCursor() => _dc;
}
