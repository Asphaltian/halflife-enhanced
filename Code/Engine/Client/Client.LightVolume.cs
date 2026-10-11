using System;

namespace GoldSrc;

partial class Client
{
	private const float LightVolumeMargin = 16f;
	private const int IrradianceResolution = 8;
	private const int IrradianceChannels = 4;
	private const int ProbeChannels = 4;
	private const int MaxLight = 255;
	private const float SamplerScale = 0.5f * MathF.PI;

	private readonly record struct LightVolumeProbe( int Index, LightSample Sample, byte[] Styles );

	private sealed class LightVolume
	{
		public Vector3Int Counts;
		public LightVolumeProbe[] Probes;
		public byte[] Styles;
		public int[] StyleValues;
		public byte[] Data;
		public Texture Irradiance;
	}

	private GameObject _lightVolumeObject;
	private LightVolume _lightVolume;

	private void ClearLightVolume()
	{
		_lightVolumeObject?.Destroy();
		_lightVolumeObject = null;

		_lightVolume?.Irradiance.Dispose();
		_lightVolume = null;
	}

	private void CreateLightVolume( string modelName )
	{
		ClearLightVolume();

		var path = Server.GetMountPath( modelName );

		if ( Texture.Find( $"{path}/relocation.vtex" ) is not { } relocation || Texture.Find( $"{path}/irradiance.vtex" ) is not { } irradiance )
			return;

		if ( Texture.Find( $"{path}/irradianceprobes.vtex" ) is { } probes )
			_lightVolume = CreateLightVolume( irradiance, probes );

		_lightVolumeObject = new GameObject( true, "light volume" ) { NetworkMode = NetworkMode.Never };

		var volume = _lightVolumeObject.AddComponent<IndirectLightVolume>();

		volume.Bounds = new BBox( WorldFile.Models[0].Mins, WorldFile.Models[0].Maxs ).Grow( LightVolumeMargin );
		volume.IrradianceTexture = _lightVolume?.Irradiance ?? irradiance;
		volume.DistanceTexture = Texture.Find( $"{path}/distance.vtex" );
		volume.RelocationTexture = relocation;
	}

	private LightVolume CreateLightVolume( Texture irradiance, Texture probes )
	{
		var records = new float[probes.Width * probes.Height * ProbeChannels];

		probes.GetPixels( (0, 0, probes.Width, probes.Height), 0, 0, records.AsSpan(), ImageFormat.RGBA32323232F );

		var list = new List<LightVolumeProbe>();

		for ( var offset = 0; offset < records.Length && records[offset + 3] != 0f; offset += ProbeChannels )
		{
			var sample = new LightSample( (int)records[offset + 1], (int)records[offset + 2] );

			list.Add( new LightVolumeProbe( (int)records[offset], sample, [.. WorldFile.Faces[sample.Face].Styles.Where( style => style != 0 && style != 255 ).Distinct()] ) );
		}

		var pixels = new Half[irradiance.Width * irradiance.Height * irradiance.Depth * IrradianceChannels];

		irradiance.GetPixels3D( (0, 0, 0, irradiance.Width, irradiance.Height, irradiance.Depth), 0, pixels.AsSpan(), ImageFormat.RGBA16161616F );

		var volume = new LightVolume
		{
			Counts = new Vector3Int( irradiance.Width / IrradianceResolution, irradiance.Height / IrradianceResolution, irradiance.Depth ),
			Probes = [.. list],
			Styles = [.. list.SelectMany( x => x.Styles ).Distinct()],
			StyleValues = new int[_lightStyleValues.Length],
			Data = new byte[pixels.Length * 2]
		};

		for ( var i = 0; i < pixels.Length; i++ )
			BitConverter.TryWriteBytes( volume.Data.AsSpan( i * 2 ), BitConverter.HalfToUInt16Bits( pixels[i] ) );

		Array.Fill( volume.StyleValues, -1 );

		volume.Irradiance = Texture.CreateVolume( irradiance.Width, irradiance.Height, irradiance.Depth, ImageFormat.RGBA16161616F )
			.WithName( "light volume irradiance" )
			.WithDynamicUsage()
			.WithData( volume.Data )
			.Finish();

		return volume;
	}

	private void UpdateLightVolume()
	{
		if ( _lightVolume is not { } volume )
			return;

		var changed = false;

		foreach ( var style in volume.Styles )
			changed |= _lightStyleValues[style] != volume.StyleValues[style];

		if ( !changed )
			return;

		foreach ( var probe in volume.Probes )
		{
			var dirty = false;

			foreach ( var style in probe.Styles )
				dirty |= _lightStyleValues[style] != volume.StyleValues[style];

			if ( dirty )
				WriteLightVolumeProbe( volume, probe );
		}

		_lightStyleValues.CopyTo( volume.StyleValues, 0 );

		volume.Irradiance.Update3D( volume.Data.AsSpan(), 0, 0, 0, volume.Counts.x * IrradianceResolution, volume.Counts.y * IrradianceResolution, volume.Counts.z );
	}

	private void WriteLightVolumeProbe( LightVolume volume, in LightVolumeProbe probe )
	{
		var (red, green, blue) = BlendLightSample( probe.Sample );

		red = Math.Min( Math.Max( red >> 8, 1 ), MaxLight );
		green = Math.Min( green >> 8, MaxLight );
		blue = Math.Min( blue >> 8, MaxLight );

		var max = Math.Max( Math.Max( red, green ), blue );
		var scale = LightGammaTable( max * 4 ) / 1023f / max;
		var counts = volume.Counts;

		Span<ushort> value = stackalloc ushort[3];

		value[0] = BitConverter.HalfToUInt16Bits( (Half)(ScreenToLinear( red * scale ) / SamplerScale) );
		value[1] = BitConverter.HalfToUInt16Bits( (Half)(ScreenToLinear( green * scale ) / SamplerScale) );
		value[2] = BitConverter.HalfToUInt16Bits( (Half)(ScreenToLinear( blue * scale ) / SamplerScale) );

		var x = probe.Index % counts.x;
		var y = probe.Index / counts.x % counts.y;
		var z = probe.Index / (counts.x * counts.y);

		for ( var v = 0; v < IrradianceResolution; v++ )
		{
			var row = ((((z * counts.y * IrradianceResolution) + (y * IrradianceResolution) + v) * counts.x * IrradianceResolution) + (x * IrradianceResolution)) * IrradianceChannels;

			for ( var u = 0; u < IrradianceResolution; u++ )
			{
				for ( var channel = 0; channel < value.Length; channel++ )
					BitConverter.TryWriteBytes( volume.Data.AsSpan( (row + (u * IrradianceChannels) + channel) * 2 ), value[channel] );
			}
		}
	}
}
