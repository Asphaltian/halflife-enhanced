using System;
using System.Text;

namespace HalfLife;

public ref struct MessageBuffer( byte[] data )
{
	private readonly byte[] _data = data;
	private int _read;

	public bool Ok { get; private set; } = true;

	public int ReadChar()
	{
		if ( _read + 1 > _data.Length )
		{
			Ok = false;
			return -1;
		}

		return (sbyte)_data[_read++];
	}

	public int ReadByte()
	{
		if ( _read + 1 > _data.Length )
		{
			Ok = false;
			return -1;
		}

		return _data[_read++];
	}

	public int ReadShort()
	{
		if ( _read + 2 > _data.Length )
		{
			Ok = false;
			return -1;
		}

		var value = BitConverter.ToInt16( _data, _read );

		_read += 2;

		return value;
	}

	public int ReadLong()
	{
		if ( _read + 4 > _data.Length )
		{
			Ok = false;
			return -1;
		}

		var value = BitConverter.ToInt32( _data, _read );

		_read += 4;

		return value;
	}

	public float ReadFloat()
	{
		if ( _read + 4 > _data.Length )
		{
			Ok = false;
			return -1f;
		}

		var value = BitConverter.ToSingle( _data, _read );

		_read += 4;

		return value;
	}

	public string ReadString()
	{
		var builder = new StringBuilder();

		while ( builder.Length < 2047 && _read + 1 <= _data.Length )
		{
			var c = ReadChar();

			if ( c is -1 or 0 )
				break;

			builder.Append( (char)(byte)c );
		}

		return builder.ToString();
	}

	public float ReadCoord() => (float)(ReadShort() * (1.0 / 8));

	public float ReadAngle() => (float)(ReadChar() * (360.0 / 256));

	public float ReadHiresAngle() => (float)(ReadShort() * (360.0 / 65536));
}
