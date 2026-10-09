using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "path_corner" )]
public class PathCorner : PointEntity
{
	public const int SpawnFlagWaitForTrigger = 0x001;
	public const int SpawnFlagTeleport = 0x002;
	public const int SpawnFlagFireOnce = 0x004;

	[Save( FieldType.Float )]
	public float Wait { get; set; }

	public override bool KeyValue( string key, string value )
	{
		if ( key == "wait" )
		{
			Wait = Util.Atof( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override float GetDelay() => Wait;

	public override void Spawn()
	{
	}
}

[LinkEntityToClass( "path_track" )]
public class PathTrack : PointEntity
{
	public const int SpawnFlagDisabled = 0x00000001;
	public const int SpawnFlagFireOnce = 0x00000002;
	public const int SpawnFlagAltReverse = 0x00000004;
	public const int SpawnFlagDisableTrain = 0x00000008;
	public const int SpawnFlagAlternate = 0x00008000;

	[Save( FieldType.String )]
	public string AltName { get; set; }
	[Save( FieldType.ClassPtr )]
	public PathTrack NextTrack { get; private set; }
	[Save( FieldType.ClassPtr )]
	public PathTrack PreviousTrack { get; private set; }
	[Save( FieldType.ClassPtr )]
	public PathTrack AltPath { get; private set; }

	public override bool KeyValue( string key, string value )
	{
		if ( key == "altpath" )
		{
			AltName = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var flag = AltPath is not null ? SpawnFlagAlternate : SpawnFlagDisabled;
		var on = (SpawnFlags & flag) == 0;

		if ( !ShouldToggle( useType, on ) )
			return;

		if ( on )
			SpawnFlags |= flag;
		else
			SpawnFlags &= ~flag;
	}

	public override void Spawn()
	{
		Solid = Solid.Trigger;
		SetSize( new Vector3( -8f, -8f, -8f ), new Vector3( 8f, 8f, 8f ) );

		NextTrack = null;
		PreviousTrack = null;
	}

	public override void Activate()
	{
		if ( !string.IsNullOrEmpty( TargetName ) )
			Link();
	}

	public static PathTrack Instance( Edict edict )
	{
		return edict is PathTrack track && track.ClassName == "path_track" ? track : null;
	}

	public static PathTrack ValidPath( PathTrack path, bool testFlag )
	{
		if ( path is null )
			return null;

		if ( testFlag && (path.SpawnFlags & SpawnFlagDisabled) != 0 )
			return null;

		return path;
	}

	public PathTrack GetNext()
	{
		if ( AltPath is not null && (SpawnFlags & SpawnFlagAlternate) != 0 && (SpawnFlags & SpawnFlagAltReverse) == 0 )
			return AltPath;

		return NextTrack;
	}

	public PathTrack GetPrevious()
	{
		if ( AltPath is not null && (SpawnFlags & SpawnFlagAlternate) != 0 && (SpawnFlags & SpawnFlagAltReverse) != 0 )
			return AltPath;

		return PreviousTrack;
	}

	public void SetPrevious( PathTrack previous )
	{
		if ( previous is not null && (previous.TargetName ?? string.Empty) != (AltName ?? string.Empty) )
			PreviousTrack = previous;
	}

	public PathTrack LookAhead( ref Vector3 origin, float dist, bool move )
	{
		var originalDist = dist;
		var current = this;
		var currentPos = origin;

		if ( dist < 0f )
		{
			dist = -dist;

			while ( dist > 0f )
			{
				var dir = current.Origin - currentPos;
				var length = MathLib.Length( dir );

				if ( length == 0f )
				{
					if ( ValidPath( current.GetPrevious(), move ) is null )
					{
						if ( !move )
							Project( current.GetNext(), current, ref origin, dist );

						return null;
					}

					current = current.GetPrevious();
				}
				else if ( length > dist )
				{
					origin = currentPos + (dir * (dist / length));
					return current;
				}
				else
				{
					dist -= length;
					currentPos = current.Origin;
					origin = currentPos;

					if ( ValidPath( current.GetPrevious(), move ) is null )
						return null;

					current = current.GetPrevious();
				}
			}

			origin = currentPos;
			return current;
		}

		while ( dist > 0f )
		{
			if ( ValidPath( current.GetNext(), move ) is null )
			{
				if ( !move )
					Project( current.GetPrevious(), current, ref origin, dist );

				return null;
			}

			var dir = current.GetNext().Origin - currentPos;
			var length = MathLib.Length( dir );

			if ( length == 0f && ValidPath( current.GetNext().GetNext(), move ) is null )
				return dist == originalDist ? null : current;

			if ( length > dist )
			{
				origin = currentPos + (dir * (dist / length));
				return current;
			}

			dist -= length;
			currentPos = current.GetNext().Origin;
			current = current.GetNext();
			origin = currentPos;
		}

		origin = currentPos;
		return current;
	}

	public PathTrack Nearest( Vector3 origin )
	{
		var delta = origin - Origin;
		delta.z = 0f;

		var minDist = MathLib.Length( delta );
		var nearest = this;
		var path = GetNext();

		var deadCount = 0;

		while ( path is not null && path != this )
		{
			deadCount++;

			if ( deadCount > 9999 )
			{
				Util.Alert( AlertType.Error, $"Bad sequence of path_tracks from {TargetName}" );
				return null;
			}

			delta = origin - path.Origin;
			delta.z = 0f;

			var dist = MathLib.Length( delta );
			if ( dist < minDist )
			{
				minDist = dist;
				nearest = path;
			}

			path = path.GetNext();
		}

		return nearest;
	}

	private void Link()
	{
		if ( !string.IsNullOrEmpty( Target ) )
		{
			if ( Server.FindEntityByString( null, "targetname", Target ) is { } target )
			{
				NextTrack = Instance( target );
				NextTrack?.SetPrevious( this );
			}
			else
			{
				Util.Alert( AlertType.Console, $"Dead end link {Target}" );
			}
		}

		if ( AltName is not null && Server.FindEntityByString( null, "targetname", AltName ) is { } alt )
		{
			AltPath = Instance( alt );
			AltPath?.SetPrevious( this );
		}
	}

	private static void Project( PathTrack start, PathTrack end, ref Vector3 origin, float dist )
	{
		if ( start is null || end is null )
			return;

		var dir = Util.Normalize( end.Origin - start.Origin );
		origin = end.Origin + (dir * dist);
	}
}
