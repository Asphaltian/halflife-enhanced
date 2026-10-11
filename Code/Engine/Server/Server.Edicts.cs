using System;
using System.Text;

namespace GoldSrc;

partial class Server
{
	public const int DefaultMaxEdicts = 1200;

	private const int MaxKeyLength = 255;
	private const int SpawnFlagNotInDeathmatch = 0x800;

	private Edict[] _edicts = [];
	private float[] _freeTime = [];
	private int _numEdicts;
	private int _maxEdicts;

	public int MaxEdicts => _maxEdicts;

	public int NumEdicts => _numEdicts;

	public static string AllocString( string value )
	{
		if ( value is null || !value.Contains( '\\' ) )
			return value;

		var result = new StringBuilder( value.Length );

		for ( var i = 0; i < value.Length; i++ )
		{
			if ( value[i] != '\\' )
			{
				result.Append( value[i] );
				continue;
			}

			i++;
			result.Append( i < value.Length && value[i] == 'n' ? '\n' : '\\' );
		}

		return result.ToString();
	}

	public Edict GetEdict( int index )
	{
		return index >= 0 && index < _numEdicts ? _edicts[index] : null;
	}

	public Edict EntityOfEntIndex( int index )
	{
		if ( index < 0 || index >= _maxEdicts || _edicts[index] is not { } edict )
			return null;

		return edict.HasPrivateData || index < MaxClients ? edict : null;
	}

	public void StoreInterpolation()
	{
		for ( var i = 0; i < _numEdicts; i++ )
			_edicts[i]?.StoreInterpolation();
	}

	public void Interpolate( float fraction )
	{
		for ( var i = 0; i < _numEdicts; i++ )
			_edicts[i]?.Interpolate( fraction );
	}

	public Edict FindEntityByString( Edict start, string field, string value )
	{
		for ( var index = (start?.Index ?? 0) + 1; index < _numEdicts; index++ )
		{
			var edict = _edicts[index];
			if ( edict is null )
				continue;

			var text = field.ToLowerInvariant() switch
			{
				"classname" => edict.ClassName,
				"globalname" => edict.GlobalName,
				"model" => edict.ModelName,
				"target" => edict.Target,
				"targetname" => edict.TargetName,
				"netname" => edict.NetName,
				"message" => edict.Message,
				"viewmodel" => edict.ViewModel,
				"weaponmodel" => edict.WeaponModel,
				"noise" => edict.Noise,
				"noise1" => edict.Noise1,
				"noise2" => edict.Noise2,
				"noise3" => edict.Noise3,
				_ => null
			};

			if ( !string.IsNullOrEmpty( text ) && text == value )
				return edict;
		}

		return null;
	}

	public Edict FindEntityInSphere( Edict start, Vector3 origin, float radius )
	{
		var radiusSquared = radius * radius;

		for ( var index = (start?.Index ?? 0) + 1; index < _numEdicts; index++ )
		{
			var edict = _edicts[index];
			if ( edict is null || string.IsNullOrEmpty( edict.ClassName ) )
				continue;

			if ( index <= MaxClients && !_clients[index - 1].Active )
				continue;

			var distanceSquared = 0f;

			for ( var i = 0; i < 3 && distanceSquared <= radiusSquared; i++ )
			{
				float delta;

				if ( origin[i] < edict.AbsMin[i] )
					delta = origin[i] - edict.AbsMin[i];
				else if ( origin[i] > edict.AbsMax[i] )
					delta = origin[i] - edict.AbsMax[i];
				else
					delta = 0f;

				distanceSquared += delta * delta;
			}

			if ( distanceSquared <= radiusSquared )
				return edict;
		}

		return null;
	}

	public T CreateEntity<T>() where T : Edict, new()
	{
		var index = AllocEdict();
		var edict = new GameObject( true, typeof( T ).Name ).AddComponent<T>();

		edict.GameObject.NetworkMode = NetworkMode.Never;
		edict.Tags.Add( ScenePhysics.Tag );

		edict.Index = index;
		_edicts[index] = edict;

		return edict;
	}

	public Edict CreateNamedEntity( string className )
	{
		var index = AllocEdict();

		if ( LinkEntityToClassAttribute.Find( className ) is null )
		{
			_freeTime[index] = (float)Time;

			Log.Info( $"Can't create entity: {className}" );
			return null;
		}

		return CreateEdict( index, className );
	}

	public void FreeEdict( Edict edict )
	{
		if ( _edicts[edict.Index] != edict )
			return;

		UnlinkEdict( edict );

		_edicts[edict.Index] = null;
		_freeTime[edict.Index] = (float)Time;

		edict.GameObject.Destroy();
	}

	private int AllocEdict()
	{
		int index;

		for ( index = MaxClients + 1; index < _numEdicts; index++ )
		{
			if ( _edicts[index] is null && (_freeTime[index] < 2f || Time - _freeTime[index] > 0.5) )
				return index;
		}

		if ( index >= _maxEdicts )
			throw new InvalidOperationException( _maxEdicts == 0 ? "ED_Alloc: No edicts yet" : "ED_Alloc: no free edicts" );

		_numEdicts++;
		return index;
	}

	private Edict CreateEdict( int index, string className )
	{
		var type = LinkEntityToClassAttribute.Find( className );
		if ( type is null )
		{
			Log.Info( $"Can't init {className}" );
			return null;
		}

		if ( _edicts[index] is { } previous )
		{
			UnlinkEdict( previous );

			previous.GameObject.Destroy();
		}

		var gameObject = new GameObject( true, className );

		gameObject.NetworkMode = NetworkMode.Never;
		gameObject.Tags.Add( ScenePhysics.Tag );

		var edict = (Edict)gameObject.Components.Create( type );

		edict.Index = index;
		edict.ClassName = className;

		_edicts[index] = edict;
		return edict;
	}

	private void InitClientEdicts()
	{
		for ( var index = 1; index <= MaxClients; index++ )
		{
			var gameObject = new GameObject( true, "edict" );

			gameObject.NetworkMode = NetworkMode.Never;

			var edict = gameObject.AddComponent<Edict>();

			edict.Index = index;

			_edicts[index] = edict;
		}
	}

	private void ClearEdicts()
	{
		foreach ( var edict in _edicts )
			edict?.GameObject.Destroy();

		_edicts = [];
		_freeTime = [];
		_numEdicts = 0;
		_maxEdicts = 0;
	}

	private void LoadFromFile( string data )
	{
		Globals.Time = (float)Time;

		var position = 0;
		var inhibited = 0;
		var world = true;

		while ( Common.Parse( data, ref position ) is string token )
		{
			if ( !token.StartsWith( '{' ) )
				throw HostError( $"ED_LoadFromFile: found {token} when expecting {{" );

			var edict = ParseEdict( data, ref position, world ? 0 : AllocEdict(), world );
			world = false;

			if ( edict is null )
				continue;

			if ( Deathmatch && (edict.SpawnFlags & SpawnFlagNotInDeathmatch) != 0 )
			{
				FreeEdict( edict );
				inhibited++;
			}
			else if ( edict.DispatchSpawn() < 0 || (edict.EntityFlags & EntityFlags.KillMe) == EntityFlags.KillMe )
			{
				FreeEdict( edict );
			}
		}

		Log.Info( $"{inhibited} entities inhibited" );
	}

	private Edict ParseEdict( string data, ref int position, int index, bool world )
	{
		var className = FindClassName( data, position );
		if ( className is null )
			return null;

		var edict = CreateEdict( index, className );

		if ( world && edict is not null )
		{
			edict.ModelName = _models[1].Name;
			edict.ModelIndex = 1;
			edict.Solid = Solid.Bsp;
			edict.MoveType = MoveType.Push;
		}

		while ( true )
		{
			var key = Common.Parse( data, ref position );
			if ( key is not null && key.StartsWith( '}' ) )
				break;

			if ( key is null )
				throw HostError( "ED_ParseEntity: EOF without closing brace" );

			if ( key.Length > MaxKeyLength )
				key = key[..MaxKeyLength];

			key = key.TrimEnd( ' ' );

			var value = Common.Parse( data, ref position );
			if ( value is null )
				throw HostError( "ED_ParseEntity: EOF without closing brace" );

			if ( value.StartsWith( '}' ) )
				throw HostError( "ED_ParseEntity: closing brace without data" );

			if ( value == className )
				continue;

			if ( key == "angle" )
			{
				var angle = Common.Atof( value );

				if ( angle >= 0f )
					value = FormattableString.Invariant( $"{edict?.Angles.x ?? 0f:F6} {angle:F6} {edict?.Angles.z ?? 0f:F6}" );
				else if ( (int)angle == -1 )
					value = "-90 0 0";
				else
					value = "90 0 0";

				key = "angles";
			}

			edict?.DispatchKeyValue( className, key, value );
		}

		return edict;
	}

	private static string FindClassName( string data, int position )
	{
		while ( Common.Parse( data, ref position ) is string key && !key.StartsWith( '}' ) )
		{
			var value = Common.Parse( data, ref position );
			if ( value is null )
				break;

			if ( key == "classname" )
				return value;
		}

		return null;
	}
}
