using System;
using System.Linq;
using System.Threading.Tasks;

namespace GoldSrc;

partial class Client
{
	private const float LightVolumeMargin = 16f;
	private const int IrradianceResolution = 8;
	private const int IrradianceTexels = IrradianceResolution * IrradianceResolution;
	private const int IrradianceChannels = 4;
	private const int ProbeRays = 1024;
	private const int HitChannels = 12;
	private const int LightTableSize = 1024;

	private readonly record struct LightVolumeHit( int Ray, LightSample Sample, Vector3 Color );

	private readonly record struct LightVolumeProbe( int Index, int FirstHit, int HitCount, byte[] Styles );

	private sealed class LightVolumeState
	{
		public int[] StyleValues;
		public Vector3[] Radiance;
		public Vector3[] Change;
		public byte[] Data;

		public void CopyTo( LightVolumeState other )
		{
			StyleValues.CopyTo( other.StyleValues, 0 );
			Radiance.CopyTo( other.Radiance, 0 );
			Change.CopyTo( other.Change, 0 );
			Data.CopyTo( other.Data, 0 );
		}
	}

	private static readonly Vector3[] ProbeDirections = [.. Enumerable.Range( 0, ProbeRays ).Select( ProbeDirection )];
	private static readonly (int Texel, float Weight)[][] IrradianceWeights = BuildIrradianceWeights();

	private GameObject _lightVolume;
	private Vector3Int _lightVolumeCounts;
	private Texture _lightVolumeIrradiance;
	private Half[] _lightVolumeBase;
	private LightVolumeHit[] _lightVolumeHits;
	private LightVolumeProbe[] _lightVolumeProbes;
	private byte[] _lightVolumeStyles;
	private LightVolumeState _lightVolumeState;
	private LightVolumeState _lightVolumePrevious;
	private bool _lightVolumeBusy;

	private static Vector3 ProbeDirection( int index )
	{
		const float goldenRatio = 1.6180339887498949f;

		var i = index + 0.5f;
		var phi = 2f * MathF.PI * goldenRatio * i;
		var cosTheta = 1f - (2f * (i / ProbeRays));
		var sinTheta = MathF.Sqrt( Math.Clamp( 1f - (cosTheta * cosTheta), 0f, 1f ) );

		return new Vector3( MathF.Cos( phi ) * sinTheta, MathF.Sin( phi ) * sinTheta, cosTheta );
	}

	private static (int Texel, float Weight)[][] BuildIrradianceWeights()
	{
		var weights = new List<(int Texel, float Weight)>[ProbeRays];

		for ( var ray = 0; ray < ProbeRays; ray++ )
			weights[ray] = [];

		for ( var texel = 0; texel < IrradianceTexels; texel++ )
		{
			var x = ((((texel % IrradianceResolution) + 0.5f) / IrradianceResolution) * 2f) - 1f;
			var y = ((((texel / IrradianceResolution) + 0.5f) / IrradianceResolution) * 2f) - 1f;
			var z = 1f - MathF.Abs( x ) - MathF.Abs( y );

			if ( z < 0f )
				(x, y) = ((1f - MathF.Abs( y )) * (x >= 0f ? 1f : -1f), (1f - MathF.Abs( x )) * (y >= 0f ? 1f : -1f));

			var direction = new Vector3( x, y, z ).Normal;
			var total = ProbeDirections.Sum( ray => MathF.Max( Vector3.Dot( direction, ray ), 0f ) );

			for ( var ray = 0; ray < ProbeRays; ray++ )
			{
				var weight = MathF.Max( Vector3.Dot( direction, ProbeDirections[ray] ), 0f );

				if ( weight > 0f )
					weights[ray].Add( (texel, weight / total) );
			}
		}

		return [.. weights.Select( x => x.ToArray() )];
	}

	private void ClearLightVolume()
	{
		_lightVolume?.Destroy();
		_lightVolume = null;

		_lightVolumeIrradiance?.Dispose();
		_lightVolumeIrradiance = null;

		_lightVolumeBase = null;
		_lightVolumeHits = null;
		_lightVolumeProbes = null;
		_lightVolumeStyles = null;
		_lightVolumeState = null;
		_lightVolumePrevious = null;
	}

	private void CreateLightVolume( string modelName )
	{
		ClearLightVolume();

		var path = Server.GetMountPath( modelName );

		if ( Texture.Find( $"{path}/relocation.vtex" ) is not { } relocation || Texture.Find( $"{path}/irradiance.vtex" ) is not { } irradiance )
			return;

		if ( Texture.Find( $"{path}/irradiancehits.vtex" ) is { } hits )
			CreateLightVolumeState( irradiance, hits );

		_lightVolume = new GameObject( true, "light volume" ) { NetworkMode = NetworkMode.Never };

		var volume = _lightVolume.AddComponent<IndirectLightVolume>();

		volume.Bounds = new BBox( WorldFile.Models[0].Mins, WorldFile.Models[0].Maxs ).Grow( LightVolumeMargin );
		volume.IrradianceTexture = _lightVolumeIrradiance ?? irradiance;
		volume.DistanceTexture = Texture.Find( $"{path}/distance.vtex" );
		volume.RelocationTexture = relocation;
	}

	private void CreateLightVolumeState( Texture irradiance, Texture hits )
	{
		var records = new float[hits.Width * hits.Height * 4];

		hits.GetPixels( (0, 0, hits.Width, hits.Height), 0, 0, records.AsSpan(), ImageFormat.RGBA32323232F );

		var offsets = Enumerable.Range( 0, records.Length / HitChannels )
			.Select( x => x * HitChannels )
			.Where( x => records[x + 4] != 0f || records[x + 5] != 0f || records[x + 6] != 0f )
			.ToArray();

		_lightVolumeHits = [.. offsets.Select( x => new LightVolumeHit( (int)records[x + 1], new LightSample( (int)records[x + 2], (int)records[x + 3] ), new Vector3( records[x + 4], records[x + 5], records[x + 6] ) ) )];

		_lightVolumeProbes = [.. Enumerable.Range( 0, offsets.Length )
			.GroupBy( x => (int)records[offsets[x]] )
			.Select( x => new LightVolumeProbe( x.Key, x.First(), x.Count(), [.. x.SelectMany( hit => WorldFile.Faces[_lightVolumeHits[hit].Sample.Face].Styles ).Where( style => style != 0 && style != 255 ).Distinct()] ) )];

		_lightVolumeStyles = [.. _lightVolumeProbes.SelectMany( x => x.Styles ).Distinct()];
		_lightVolumeCounts = new Vector3Int( irradiance.Width / IrradianceResolution, irradiance.Height / IrradianceResolution, irradiance.Depth );
		_lightVolumeBase = new Half[irradiance.Width * irradiance.Height * irradiance.Depth * IrradianceChannels];

		irradiance.GetPixels3D( (0, 0, 0, irradiance.Width, irradiance.Height, irradiance.Depth), 0, _lightVolumeBase.AsSpan(), ImageFormat.RGBA16161616F );

		_lightVolumeState = NewLightVolumeState();
		_lightVolumePrevious = NewLightVolumeState();

		for ( var i = 0; i < offsets.Length; i++ )
			_lightVolumeState.Radiance[i] = new Vector3( records[offsets[i] + 8], records[offsets[i] + 9], records[offsets[i] + 10] );

		for ( var i = 0; i < _lightVolumeBase.Length; i++ )
			BitConverter.TryWriteBytes( _lightVolumeState.Data.AsSpan( i * 2 ), BitConverter.HalfToUInt16Bits( _lightVolumeBase[i] ) );

		Array.Fill( _lightVolumeState.StyleValues, -1 );

		RelightVolume( _lightVolumeState, _lightStyleValues );

		_lightVolumeState.CopyTo( _lightVolumePrevious );

		_lightVolumeIrradiance = Texture.CreateVolume( irradiance.Width, irradiance.Height, irradiance.Depth, ImageFormat.RGBA16161616F )
			.WithName( "light volume irradiance" )
			.WithDynamicUsage()
			.WithData( _lightVolumeState.Data )
			.Finish();
	}

	private LightVolumeState NewLightVolumeState()
	{
		return new LightVolumeState
		{
			StyleValues = new int[256],
			Radiance = new Vector3[_lightVolumeHits.Length],
			Change = new Vector3[_lightVolumeProbes.Length * IrradianceTexels],
			Data = new byte[_lightVolumeBase.Length * 2]
		};
	}

	private bool LightVolumeStylesMatch( LightVolumeState state )
	{
		foreach ( var style in _lightVolumeStyles )
		{
			if ( _lightStyleValues[style] != state.StyleValues[style] )
				return false;
		}

		return true;
	}

	private void RelightVolume( LightVolumeState state, int[] styleValues )
	{
		var changed = new bool[styleValues.Length];
		var light = new float[LightTableSize];

		for ( var style = 0; style < changed.Length; style++ )
			changed[style] = styleValues[style] != state.StyleValues[style];

		for ( var i = 0; i < light.Length; i++ )
			light[i] = MathF.Floor( LightGammaTable( i ) / 4f ) / 255f * (128f / 192f) * 2f;

		for ( var index = 0; index < _lightVolumeProbes.Length; index++ )
		{
			var probe = _lightVolumeProbes[index];

			if ( !probe.Styles.Any( style => changed[style] ) )
				continue;

			var change = state.Change.AsSpan( index * IrradianceTexels, IrradianceTexels );

			for ( var i = probe.FirstHit; i < probe.FirstHit + probe.HitCount; i++ )
			{
				var hit = _lightVolumeHits[i];
				var styles = WorldFile.Faces[hit.Sample.Face].Styles;

				if ( !changed[styles[0]] && !changed[styles[1]] && !changed[styles[2]] && !changed[styles[3]] )
					continue;

				var (red, green, blue) = BlendLightSample( hit.Sample, styleValues );

				var radiance = new Vector3(
					ScreenToLinear( MathF.Min( hit.Color.x * light[Math.Min( red >> 6, LightTableSize - 1 )], 1f ) ),
					ScreenToLinear( MathF.Min( hit.Color.y * light[Math.Min( green >> 6, LightTableSize - 1 )], 1f ) ),
					ScreenToLinear( MathF.Min( hit.Color.z * light[Math.Min( blue >> 6, LightTableSize - 1 )], 1f ) ) );

				var difference = radiance - state.Radiance[i];

				state.Radiance[i] = radiance;

				foreach ( var (texel, weight) in IrradianceWeights[hit.Ray] )
					change[texel] += difference * weight;
			}

			WriteLightVolumeProbe( probe.Index, change, state.Data );
		}

		styleValues.CopyTo( state.StyleValues, 0 );
	}

	private void WriteLightVolumeProbe( int probe, Span<Vector3> change, byte[] data )
	{
		var counts = _lightVolumeCounts;
		var x = probe % counts.x;
		var y = probe / counts.x % counts.y;
		var z = probe / (counts.x * counts.y);

		for ( var v = 0; v < IrradianceResolution; v++ )
		{
			var row = ((((z * counts.y * IrradianceResolution) + (y * IrradianceResolution) + v) * counts.x * IrradianceResolution) + (x * IrradianceResolution)) * IrradianceChannels;

			for ( var u = 0; u < IrradianceResolution; u++ )
			{
				var edgeU = u == 0 || u == IrradianceResolution - 1;
				var edgeV = v == 0 || v == IrradianceResolution - 1;
				var value = change[(v * IrradianceResolution) + u];

				if ( edgeU || edgeV )
					value = (value + change[((edgeU ? IrradianceResolution - 1 - v : v) * IrradianceResolution) + (edgeV ? IrradianceResolution - 1 - u : u)]) * 0.5f;

				var texel = row + (u * IrradianceChannels);

				for ( var channel = 0; channel < 3; channel++ )
					BitConverter.TryWriteBytes( data.AsSpan( (texel + channel) * 2 ), BitConverter.HalfToUInt16Bits( (Half)MathF.Max( (float)_lightVolumeBase[texel + channel] + value[channel], 0f ) ) );
			}
		}
	}

	private void UpdateLightVolume()
	{
		if ( _lightVolumeState is null || _lightVolumeBusy || LightVolumeStylesMatch( _lightVolumeState ) )
			return;

		if ( LightVolumeStylesMatch( _lightVolumePrevious ) )
			ShowLightVolumeState( _lightVolumePrevious, _lightVolumeState );
		else
			_ = RelightVolumeAsync( _lightVolumeState, _lightVolumePrevious, [.. _lightStyleValues] );
	}

	private async Task RelightVolumeAsync( LightVolumeState current, LightVolumeState next, int[] styleValues )
	{
		_lightVolumeBusy = true;

		await GameTask.RunInThreadAsync( () =>
		{
			current.CopyTo( next );

			RelightVolume( next, styleValues );
		} );

		_lightVolumeBusy = false;

		if ( _lightVolumeState == current )
			ShowLightVolumeState( next, current );
	}

	private void ShowLightVolumeState( LightVolumeState state, LightVolumeState previous )
	{
		var counts = _lightVolumeCounts;

		_lightVolumeState = state;
		_lightVolumePrevious = previous;

		_lightVolumeIrradiance.Update3D( state.Data.AsSpan(), 0, 0, 0, counts.x * IrradianceResolution, counts.y * IrradianceResolution, counts.z );
	}
}
