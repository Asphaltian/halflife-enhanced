namespace GoldSrc.GameUI;

public sealed class BitmapImagePanel : Panel
{
	private static readonly Color32 White = new( 255, 255, 255, 255 );

	private string _textureName = "";
	private bool _usedTexture;
	private Texture _texture;

	public BitmapImagePanel( Vgui vgui, Panel parent, string name, string fileName = null ) : base( vgui, parent, name )
	{
		SetBounds( 0, 0, 100, 100 );

		if ( !string.IsNullOrEmpty( fileName ) )
			_textureName = fileName;
	}

	public void SetTexture( string fileName )
	{
		_textureName = fileName;

		if ( _usedTexture )
		{
			ForceReload();
			return;
		}

		if ( _textureName.Length == 0 )
			return;

		_usedTexture = true;
		_texture = Vgui.Surface.LoadImage( _textureName, true );
	}

	public void ForceReload()
	{
		if ( _usedTexture )
			_texture = Vgui.Surface.LoadImage( _textureName, true );
	}

	public override void PaintBackground()
	{
		if ( _textureName.Length == 0 )
			return;

		if ( !_usedTexture )
		{
			_usedTexture = true;
			_texture = Vgui.Surface.LoadImage( _textureName, true );
		}

		Vgui.Surface.DrawSetColor( White );
		Vgui.Surface.DrawTexturedRect( _texture, 0, 0, Wide, Tall );
	}
}
