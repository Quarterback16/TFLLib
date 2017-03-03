using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace TFLLib.Serialisation
{
   /// <summary>
   /// Defines an XML serializer that uses the <see cref="NetDataContractSerializer"/>.
   /// </summary>
   public class XmlSerializer : ISerializer
   {
      private Encoding DefaultEncoding = Encoding.UTF8;

      /// <summary>
      /// Serialize data.
      /// </summary>
      /// <param name="data">Data to serialize.</param>
      /// <param name="encoding">The encoding to use.</param>
      /// <returns>The serialized data.</returns>
      public string Serialize( object data, Encoding encoding = null )
      {
         if ( encoding == null )
         {
            encoding = DefaultEncoding;
         }

         if ( data == null )
         {
            data = new NullObject();
         }

         string result;
         using ( var outerMmemoryStream = new MemoryStream() )
         {
            using ( var xmlDictionaryWriter = XmlDictionaryWriter.CreateTextWriter( outerMmemoryStream, encoding ) )
            {
               var netDataContractSerializer = new NetDataContractSerializer();

               netDataContractSerializer.WriteObject( xmlDictionaryWriter, data );
            }

            using ( var innerMemoryStream = new MemoryStream( outerMmemoryStream.ToArray() ) )
            {
               var streamReader = new StreamReader( innerMemoryStream, encoding );
               result = streamReader.ReadToEnd();
            }
         }

         return result;
      }

      /// <summary>
      /// Deserialize data.
      /// </summary>
      /// <param name="data">Data to deserialize.</param>
      /// <param name="encoding">The encoding to use.</param>
      /// <returns>The deserialized data.</returns>
      public object Deserialize( string data, Encoding encoding = null )
      {
         if ( encoding == null )
         {
            encoding = DefaultEncoding;
         }

         if ( data == null )
         {
            return null;
         }

         var bytes = encoding.GetBytes( data );

         if ( bytes == null || bytes.Length == 0 )
         {
            return null;
         }

         object result;
         using ( var memoryStream = new MemoryStream( bytes ) )
         {
            using ( var xmlDictionaryReader = XmlDictionaryReader.CreateTextReader( memoryStream, encoding, XmlDictionaryReaderQuotas.Max, null ) )
            {
               var netDataContractSerializer = new NetDataContractSerializer();

               result = netDataContractSerializer.ReadObject( xmlDictionaryReader, true );
            }
         }

         if ( result != null && result.GetType() == typeof( NullObject ) )
         {
            return null;
         }

         return result;
      }
   }
}
