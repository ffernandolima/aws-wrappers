using System.IO;

namespace AwsWrappers
{
	public class DirectoryHelper
	{
		public static void CreateDirectoryIfNotExists(string path)
		{
			var directoryInfo = new DirectoryInfo(path);

			if (!directoryInfo.Exists)
			{
				Directory.CreateDirectory(path);
			}
		}
	}
}
