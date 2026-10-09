using System;

namespace GoldSrc;

partial class Client
{
	private const int IrradianceResolution = 8;
	private const int MinProbes = 4;
	private const int MaxProbes = 40;
	private const int MaxProbeDensity = 15;
	private const int IrradianceTexelSize = 8;
	private const float LightTraceDistance = 8192f;
	private const int RelocationChannels = 4;

	private readonly int[] _lightVolumeStyleValues = new int[256];

	private GameObject _lightVolume;
	private Texture _irradiance;
	private byte[] _irradianceData;
	private LightSample?[] _lightVolumeSamples;
	private Vector3Int _lightVolumeCounts;

	private void CreateLightVolume( string modelName )
	{
		_lightVolume?.Destroy();
		_lightVolume = null;
		_irradiance?.Dispose();
		_irradiance = null;

		var path = Server.GetMountPath( modelName );

		if ( WorldFile.Nodes.Length == 0 || WorldFile.Lighting.Length == 0 || Texture.Find( $"{path}/relocation.vtex" ) is not { } relocation )
			return;

		var world = WorldFile.Models[0];
		var bounds = new BBox( world.Mins, world.Maxs );
		var counts = new Vector3Int( relocation.Width, relocation.Height, relocation.Depth );

		for ( var density = MaxProbeDensity; density >= 1; density-- )
		{
			if ( ProbeCount( bounds.Size.x, density ) != counts.x || ProbeCount( bounds.Size.y, density ) != counts.y || ProbeCount( bounds.Size.z, density ) != counts.z )
				continue;

			BuildLightVolumeSamples( bounds, counts, relocation );

			_lightVolume = new GameObject( true, "light volume" );
			_lightVolume.NetworkMode = NetworkMode.Never;

			var volume = _lightVolume.AddComponent<IndirectLightVolume>();

			volume.Bounds = bounds;
			volume.ProbeDensity = density;
			volume.IrradianceTexture = _irradiance;
			volume.DistanceTexture = Texture.Find( $"{path}/distance.vtex" );
			volume.RelocationTexture = relocation;

			return;
		}
	}

	private static int ProbeCount( float size, int density )
	{
		return Math.Clamp( (int)MathF.Ceiling( size * (density / 1024f) ) + 1, MinProbes, MaxProbes );
	}

	private void BuildLightVolumeSamples( BBox bounds, Vector3Int counts, Texture relocation )
	{
		var offsets = new Half[counts.x * counts.y * counts.z * RelocationChannels];

		relocation.GetPixels3D( (0, 0, 0, counts.x, counts.y, counts.z), 0, offsets.AsSpan(), ImageFormat.RGBA16161616F );

		var spacing = new Vector3( bounds.Size.x / (counts.x - 1), bounds.Size.y / (counts.y - 1), bounds.Size.z / (counts.z - 1) );

		_lightVolumeCounts = counts;
		_lightVolumeSamples = new LightSample?[counts.x * counts.y * counts.z];
		_irradianceData = new byte[counts.x * IrradianceResolution * counts.y * IrradianceResolution * counts.z * IrradianceTexelSize];

		for ( var z = 0; z < counts.z; z++ )
		{
			for ( var y = 0; y < counts.y; y++ )
			{
				for ( var x = 0; x < counts.x; x++ )
				{
					var probe = (((z * counts.y) + y) * counts.x) + x;
					var offset = probe * RelocationChannels;

					if ( (float)offsets[offset + 3] <= 0.5f )
						continue;

					var position = bounds.Mins + (new Vector3( x, y, z ) * spacing) + new Vector3( (float)offsets[offset], (float)offsets[offset + 1], (float)offsets[offset + 2] );

					_lightVolumeSamples[probe] = RecursiveLightSample( 0, position, position - new Vector3( 0f, 0f, LightTraceDistance ) );

					WriteLightVolumeProbe( probe, Vector3.Zero );
				}
			}
		}

		_irradiance = Texture.CreateVolume( counts.x * IrradianceResolution, counts.y * IrradianceResolution, counts.z, ImageFormat.RGBA16161616F )
			.WithName( "light volume irradiance" )
			.WithDynamicUsage()
			.WithData( _irradianceData )
			.Finish();

		Array.Fill( _lightVolumeStyleValues, -1 );
	}

	private void WriteLightVolumeProbe( int probe, Vector3 light )
	{
		var counts = _lightVolumeCounts;
		var x = probe % counts.x;
		var y = probe / counts.x % counts.y;
		var z = probe / (counts.x * counts.y);

		for ( var v = 0; v < IrradianceResolution; v++ )
		{
			var row = ((((z * counts.y * IrradianceResolution) + (y * IrradianceResolution) + v) * counts.x * IrradianceResolution) + (x * IrradianceResolution)) * IrradianceTexelSize;

			for ( var u = 0; u < IrradianceResolution; u++ )
			{
				var texel = row + (u * IrradianceTexelSize);

				WriteHalf( texel, light.x );
				WriteHalf( texel + 2, light.y );
				WriteHalf( texel + 4, light.z );
				WriteHalf( texel + 6, 1f );
			}
		}
	}

	private void WriteHalf( int offset, float value )
	{
		var bits = BitConverter.HalfToUInt16Bits( (Half)value );

		_irradianceData[offset] = (byte)bits;
		_irradianceData[offset + 1] = (byte)(bits >> 8);
	}

	private static float LightVolumeScale( int value )
	{
		const float displayGamma = 2.2f;

		var index = MathF.Min( MathF.Floor( value / 64f ), 1023f );
		var screen = MathF.Floor( Client.LightGammaTable( index ) / 4f ) / 255f;

		return MathF.Pow( screen * (128f / 192f) * 2f, displayGamma );
	}

	private bool LightVolumeStyleChanged( int face )
	{
		var styles = WorldFile.Faces[face].Styles;

		for ( var map = 0; map < Bsp.Face.MaxLightmaps && styles[map] != 255; map++ )
		{
			if ( _lightStyleValues[styles[map]] != _lightVolumeStyleValues[styles[map]] )
				return true;
		}

		return false;
	}

	private void UpdateLightVolume()
	{
		if ( _irradiance is null || _lightStyleValues.AsSpan().SequenceEqual( _lightVolumeStyleValues ) )
			return;

		var counts = _lightVolumeCounts;
		var slice = counts.x * counts.y;
		var first = counts.z;
		var last = -1;

		for ( var probe = 0; probe < _lightVolumeSamples.Length; probe++ )
		{
			if ( _lightVolumeSamples[probe] is not { } sample || !LightVolumeStyleChanged( sample.Face ) )
				continue;

			var (red, green, blue) = BlendLightSample( sample );

			WriteLightVolumeProbe( probe, new Vector3( LightVolumeScale( red ), LightVolumeScale( green ), LightVolumeScale( blue ) ) / (0.5f * MathF.PI) );

			first = Math.Min( first, probe / slice );
			last = Math.Max( last, probe / slice );
		}

		_lightStyleValues.CopyTo( _lightVolumeStyleValues, 0 );

		if ( last < first )
			return;

		var width = counts.x * IrradianceResolution;
		var height = counts.y * IrradianceResolution;
		var sliceSize = width * height * IrradianceTexelSize;

		_irradiance.Update3D( _irradianceData.AsSpan( first * sliceSize, (last - first + 1) * sliceSize ), 0, 0, first, width, height, last - first + 1 );
	}
}
