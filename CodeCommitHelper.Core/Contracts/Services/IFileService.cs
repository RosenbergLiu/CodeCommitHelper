namespace CodeCommitHelper.Core.Contracts.Services;

public interface IFileService
{
    T Read<T>(string folderPath, string fileName);

    void Save<T>(string folderPath, string fileName, T content);

    void Delete(string folderPath, string fileName);

    string ReadAsString(string folderPath, string fileName);

    void SaveAsString(string folderPath, string fileName, string content);
}
