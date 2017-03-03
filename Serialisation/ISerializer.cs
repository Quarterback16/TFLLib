using System.Text;

namespace TFLLib.Serialisation
{
   public interface ISerializer
   {
      /// <summary>
      /// Serialize data.
      /// </summary>
      /// <param name="data">Data to serialize.</param>
      /// <param name="encoding">The encoding to use.</param>
      /// <returns>The serialized data.</returns>
      string Serialize( object data, Encoding encoding = null );

      /// <summary>
      /// Deserialize data.
      /// </summary>
      /// <param name="data">Data to deserialize.</param>
      /// <param name="encoding">The encoding to use.</param>
      /// <returns>The deserialized data.</returns>
      object Deserialize( string data, Encoding encoding = null );
   }
}
