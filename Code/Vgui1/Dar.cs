namespace GoldSrc.Vgui1;

public sealed class Dar<T>
{
	private const int InitialCapacity = 4;

	private int _count;
	private int _capacity;
	private T[] _data;

	public Dar()
	{
		EnsureCapacity( InitialCapacity );
	}

	public Dar( int initialCapacity )
	{
		EnsureCapacity( initialCapacity );
	}

	public void EnsureCapacity( int wantedCapacity )
	{
		if ( wantedCapacity <= _capacity )
			return;

		var newCapacity = _capacity;

		if ( newCapacity == 0 )
			newCapacity = 1;

		while ( newCapacity < wantedCapacity )
			newCapacity *= 2;

		var newData = new T[newCapacity];

		_capacity = newCapacity;

		for ( var i = 0; i < _count; i++ )
			newData[i] = _data[i];

		_data = newData;
	}

	public void SetCount( int count )
	{
		if ( count < 0 || count > _capacity )
			return;

		_count = count;
	}

	public int GetCount() => _count;

	public void AddElement( T elem )
	{
		EnsureCapacity( _count + 1 );

		_data[_count] = elem;
		_count++;
	}

	public bool HasElement( T elem )
	{
		for ( var i = 0; i < _count; i++ )
		{
			if ( EqualityComparer<T>.Default.Equals( _data[i], elem ) )
				return true;
		}

		return false;
	}

	public void PutElement( T elem )
	{
		if ( HasElement( elem ) )
			return;

		AddElement( elem );
	}

	public void InsertElementAt( T elem, int index )
	{
		if ( index < 0 || index > _count )
			return;

		if ( index == _count || _count == 0 )
		{
			AddElement( elem );
			return;
		}

		AddElement( elem );

		for ( var i = _count - 1; i > index; i-- )
			_data[i] = _data[i - 1];

		_data[index] = elem;
	}

	public void SetElementAt( T elem, int index )
	{
		if ( index < 0 || index >= _count )
			return;

		_data[index] = elem;
	}

	public void RemoveElementAt( int index )
	{
		if ( index < 0 || index >= _count )
			return;

		for ( var i = index; i < _count - 1; i++ )
			_data[i] = _data[i + 1];

		_count--;
	}

	public void RemoveElement( T elem )
	{
		for ( var i = 0; i < _count; i++ )
		{
			if ( EqualityComparer<T>.Default.Equals( _data[i], elem ) )
			{
				RemoveElementAt( i );
				break;
			}
		}
	}

	public void RemoveAll()
	{
		_count = 0;
	}

	public T this[int index] => index < 0 || index >= _count ? default : _data[index];
}
