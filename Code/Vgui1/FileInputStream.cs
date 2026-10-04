namespace GoldSrc.Vgui1;

public class FileInputStream : InputStream
{
	private const byte CarriageReturn = 0x0d;
	private const byte LineFeed = 0x0a;
	private const byte EndOfFile = 0x1a;

	private readonly byte[] _data;
	private readonly bool _textMode;
	private int _position;

	public FileInputStream( string fileName, bool textMode )
	{
		_data = Server.ReadRootFile( fileName );
		_textMode = textMode;
	}

	private bool Seek( int position )
	{
		if ( position < 0 )
			return true;

		_position = position;

		return false;
	}

	public override void SeekStart( out bool success )
	{
		if ( _data is null )
		{
			success = false;
			return;
		}

		success = Seek( 0 );
	}

	public override void SeekRelative( int count, out bool success )
	{
		if ( _data is null )
		{
			success = false;
			return;
		}

		success = Seek( _position + count );
	}

	public override void SeekEnd( out bool success )
	{
		if ( _data is null )
		{
			success = false;
			return;
		}

		success = Seek( _data.Length );
	}

	public override int GetAvailable( out bool success )
	{
		success = false;

		return 0;
	}

	private bool Read( out byte value )
	{
		value = 0;

		if ( _position >= _data.Length )
			return false;

		if ( !_textMode )
		{
			value = _data[_position++];
			return true;
		}

		if ( _data[_position] == EndOfFile )
			return false;

		if ( _data[_position] == CarriageReturn && _position + 1 < _data.Length && _data[_position + 1] == LineFeed )
			_position++;

		value = _data[_position++];

		return true;
	}

	public override byte ReadUChar( out bool success )
	{
		if ( _data is null )
		{
			success = false;
			return 0;
		}

		success = Read( out var value );

		return value;
	}

	public override void ReadUChar( byte[] buf, int count, out bool success )
	{
		if ( _data is null )
		{
			success = false;
			return;
		}

		success = true;

		for ( var i = 0; i < count; i++ )
		{
			if ( !Read( out buf[i] ) )
			{
				success = false;
				break;
			}
		}
	}

	public override void Close( out bool success )
	{
		success = _data is not null;
	}

	public virtual void Close() => Close( out _ );
}
