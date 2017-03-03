using System;
using System.Runtime.Serialization;

namespace TFLLib.Serialisation
{
   /// <summary>
   /// Defines a null object.
   /// </summary>
   [Serializable]
   internal class NullObject : ISerializable
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="NullObject"/> class.
      /// </summary>
      public NullObject() { }

      /// <summary>
      /// Initializes a new instance of the <see cref="NullObject"/> class.
      /// </summary>
      /// <param name="info">The serialization information.</param>
      /// <param name="context">The streaming context.</param>
      protected NullObject( SerializationInfo info, StreamingContext context ) { }

      /// <summary>
      /// Populates a System.Runtime.Serialization.SerializationInfo with the data needed to serialize the target object.
      /// </summary>
      /// <param name="info">The serialization information to populate with data.</param>
      /// <param name="context">The destination streaming context for this deserialization.</param>
      public virtual void GetObjectData( SerializationInfo info, StreamingContext context ) { }
   }
}
