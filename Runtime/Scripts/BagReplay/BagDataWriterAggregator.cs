using System.IO;
using UnityEngine;

namespace BagReplay
{
    public class BagDataWriterAggregator : MonoBehaviour
    {
        public void WriteAll(string path)
        {
            foreach (var bagDataWriter in GetComponents<BagDataWriter>())
            {
                var correctFileNamePath = path.Replace(Path.GetFileName(path), Path.GetFileNameWithoutExtension(bagDataWriter.sourceFilePath));
                bagDataWriter.WriteFile(correctFileNamePath);
            }
        }
    }
}