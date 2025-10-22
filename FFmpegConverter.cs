using System;
using FFmpeg.AutoGen;

public class FFmpegConverter
{
        public unsafe void Convert(
        string inputFile,
        string outputFile,
        string videoCodec,
        string audioCodec,
        string bitrate,
        Action<ConversionProgress>? progressCallback = null)
    {
        // Инициализация FFmpeg
        // ffmpeg.av_register_all(); // Устарело в новых версиях FFmpeg
        ffmpeg.avformat_network_init();

        // Открытие входного файла
        AVFormatContext* inputFormatContext = null;
        ffmpeg.avformat_open_input(&inputFormatContext, inputFile, null, null);
        ffmpeg.avformat_find_stream_info(inputFormatContext, null);

        // Поиск потоков
        int videoStreamIndex = ffmpeg.av_find_best_stream(inputFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
        int audioStreamIndex = ffmpeg.av_find_best_stream(inputFormatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);

        // Создание выходного контекста
        AVFormatContext* outputFormatContext = null;
        ffmpeg.avformat_alloc_output_context2(&outputFormatContext, null, null, outputFile);

        // Добавление видеопотока
        AVCodec* videoCodecPtr = GetVideoCodec(videoCodec);
        AVStream* videoStreamOut = ffmpeg.avformat_new_stream(outputFormatContext, videoCodecPtr);

        // Добавление аудиопотока
        AVCodec* audioCodecPtr = GetAudioCodec(audioCodec);
        AVStream* audioStreamOut = ffmpeg.avformat_new_stream(outputFormatContext, audioCodecPtr);

        // Открытие выходного файла
        ffmpeg.avio_open2(&outputFormatContext->pb, outputFile, ffmpeg.AVIO_FLAG_WRITE, null, null);
        ffmpeg.avformat_write_header(outputFormatContext, null);

        // Основной цикл конвертации
        AVPacket packet;
        while (ffmpeg.av_read_frame(inputFormatContext, &packet) >= 0)
        {
            if (packet.stream_index == videoStreamIndex)
            {
                // Обработка видеокадра
                progressCallback?.Invoke(new ConversionProgress
                {
                    Percent = (int)(100 * packet.pts * ffmpeg.av_q2d(inputFormatContext->streams[videoStreamIndex]->time_base)),
                    CurrentTime = TimeSpan.FromSeconds(packet.pts * ffmpeg.av_q2d(inputFormatContext->streams[videoStreamIndex]->time_base))
                });
            }
            ffmpeg.av_packet_unref(&packet);
        }

        // Запись трейлера и закрытие
        ffmpeg.av_write_trailer(outputFormatContext);
        ffmpeg.avformat_close_input(&inputFormatContext);
        ffmpeg.avio_closep(&outputFormatContext->pb);
        ffmpeg.avformat_free_context(outputFormatContext);
    }

    private unsafe AVCodec* GetVideoCodec(string codecName)
    {
        return codecName switch
        {
            "AV1" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AV1),
            "VP9" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_VP9),
            "H.265" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_HEVC),
            "H.266" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_VVC),
            _ => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_NONE)
        };
    }

    private unsafe AVCodec* GetAudioCodec(string codecName)
    {
        return codecName switch
        {
            "Opus" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_OPUS),
            "AAC" => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC),
            _ => ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_NONE)
        };
    }
}

public class ConversionProgress
{
    public int Percent { get; set; }
    public TimeSpan CurrentTime { get; set; }
}