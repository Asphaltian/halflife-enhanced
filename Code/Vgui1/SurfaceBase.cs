namespace GoldSrc.Vgui1;

public class ImagePanel : Panel
{
	protected Image _image;

	public ImagePanel( Image image ) : base( 0, 0, 64, 64 )
	{
		SetImage( image );
	}

	public virtual void SetImage( Image image )
	{
		_image = image;

		if ( image is not null )
		{
			image.GetSize( out var wide, out var tall );
			SetSize( wide, tall );
		}

		Repaint();
	}

	public override void PaintBackground()
	{
		if ( _image is null )
			return;

		DrawSetColor( SchemeColor.White );
		_image.DoPaint( this );
	}
}

public abstract class SurfaceBase
{
	private sealed class EmulatedCursor : ImagePanel
	{
		public EmulatedCursor() : base( null )
		{
		}

		public override bool IsWithin( int x, int y ) => false;
	}

	internal bool _needsSwap;

	protected Panel _embeddedPanel;
	protected readonly List<(int Wide, int Tall, int Bpp)> _modeInfoDar = [];
	protected ImagePanel _emulatedCursor;
	protected Cursor _currentCursor;

	protected SurfaceBase( Panel embeddedPanel )
	{
		_embeddedPanel = embeddedPanel;
		_needsSwap = true;
		_emulatedCursor = new EmulatedCursor();
		_emulatedCursor.SetVisible( false );
		_currentCursor = null;

		_embeddedPanel.SetSurfaceBaseTraverse( this );
		GetApp().SurfaceBaseCreated( this );
		_emulatedCursor.SetParent( GetPanel() );
	}

	public virtual Panel GetPanel() => _embeddedPanel;

	public virtual void RequestSwap()
	{
		_needsSwap = true;
	}

	public virtual void ResetModeInfo()
	{
		_modeInfoDar.Clear();
	}

	public virtual int GetModeInfoCount() => _modeInfoDar.Count;

	public virtual bool GetModeInfo( int mode, out int wide, out int tall, out int bpp )
	{
		wide = 0;
		tall = 0;
		bpp = 0;

		if ( mode < 0 || mode >= _modeInfoDar.Count )
			return false;

		(wide, tall, bpp) = _modeInfoDar[mode];

		return true;
	}

	public virtual App GetApp() => App.GetInstance();

	public virtual void SetEmulatedCursorVisible( bool state )
	{
		_emulatedCursor.SetVisible( state );
	}

	public virtual void SetEmulatedCursorPos( int x, int y )
	{
		GetPanel().RemoveChild( _emulatedCursor );
		GetPanel().AddChild( _emulatedCursor );
		GetPanel().ScreenToLocal( ref x, ref y );

		if ( _currentCursor is not null && !_emulatedCursor.IsVisible() )
		{
			_currentCursor.GetHotspot( out var hotspotX, out var hotspotY );

			x -= hotspotX;
			y -= hotspotY;
		}

		_emulatedCursor.SetPos( x, y );
	}

	public abstract void SetTitle( string title );
	public abstract bool SetFullscreenMode( int wide, int tall, int bpp );
	public abstract void SetWindowedMode();
	public abstract void SetAsTopMost( bool state );
	public abstract void CreatePopup( Panel embeddedPanel );
	public abstract bool HasFocus();
	public abstract bool IsWithin( int x, int y );
	public abstract int CreateNewTextureId();
	public abstract void GetMousePos( out int x, out int y );

	protected virtual void AddModeInfo( int wide, int tall, int bpp )
	{
		_modeInfoDar.Add( (wide, tall, bpp) );
	}

	public abstract void DrawSetColor( int r, int g, int b, int a );
	public abstract void DrawFilledRect( int x0, int y0, int x1, int y1 );
	public abstract void DrawOutlinedRect( int x0, int y0, int x1, int y1 );
	public abstract void DrawSetTextFont( Font font );
	public abstract void DrawSetTextColor( int r, int g, int b, int a );
	public abstract void DrawSetTextPos( int x, int y );
	public abstract void DrawPrintText( string text, int textLen );
	public abstract void DrawSetTextureRgba( int id, byte[] rgba, int wide, int tall );
	public abstract void DrawSetTexture( int id );
	public abstract void DrawTexturedRect( int x0, int y0, int x1, int y1 );
	public abstract void Invalidate( Panel panel );
	public abstract void EnableMouseCapture( bool state );
	public abstract void SetCursor( Cursor cursor );
	public abstract void SwapBuffers();
	public abstract void PushMakeCurrent( Panel panel, bool useInsets );
	public abstract void PopMakeCurrent( Panel panel );
	public abstract void ApplyChanges();
}
