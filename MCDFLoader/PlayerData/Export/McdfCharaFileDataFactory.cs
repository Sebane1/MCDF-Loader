using McdfLoader.API.Data;
using McdfLoader.FileCache;

namespace McdfLoader.PlayerData.Export;

internal sealed class McdfCharaFileDataFactory
{
    private readonly FileCacheManager _fileCacheManager;

    public McdfCharaFileDataFactory(FileCacheManager fileCacheManager)
    {
        _fileCacheManager = fileCacheManager;
    }

    public McdfCharaFileData Create(string description, CharacterData characterCacheDto)
    {
        return new McdfCharaFileData(_fileCacheManager, description, characterCacheDto);
    }
}