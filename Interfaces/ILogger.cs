namespace TFLLib.Interfaces
{
	public interface ILogger
   {
      /// <summary>
      ///     Gets this logger's name.
      /// </summary>
      string Name { get; set; }

      /// <summary>
      /// Gets the logging level.
      /// </summary>
      LogLevel Level { get; }

      #region Debug


      /// <summary>
      ///     Logs a debug message (<see cref="LogLevel.Debug" />).
      /// </summary>
      /// <param name="message">The message to log.</param>
      void Debug( string message );



      #endregion

      #region Information

      /// <summary>
      ///     Logs an Informationrmational message (<see cref="LogLevel.Information" />).
      /// </summary>
      /// <param name="message">The message to log.</param>
      void Information( string message );

      #endregion

      #region Warning

      /// <summary>
      ///     Logs a Warninging message (<see cref="LogLevel.Warn" />).
      /// </summary>
      /// <param name="message">The message to log.</param>
      void Warning( string message );


      #endregion

      #region Error

      /// <summary>
      ///     Logs an error message (<see cref="LogLevel.Error" />).
      /// </summary>
      /// <param name="message">The message to log.</param>
      void Error( string message );

      #endregion

      #region Fatal

      /// <summary>
      ///     Logs a fatal error message (<see cref="LogLevel.Fatal" />).
      /// </summary>
      /// <param name="message">The message to log.</param>
      void Fatal( string message );

      #endregion


   }

   public enum LogLevel
   {
      /// <summary>
      /// The logging level is undefined. This is regarded
      /// an invalid value.
      /// </summary>
      Undefined = 0,
      /// <summary>
      /// Logs debugging output.
      /// </summary>
      Debug = 1,
      /// <summary>
      /// Logs basic information.
      /// </summary>
      Information = 2,
      /// <summary>
      /// Logs a warning.
      /// </summary>
      Warn = 3,
      /// <summary>
      /// Logs an error.
      /// </summary>
      Error = 4,
      /// <summary>
      /// Logs a fatal incident.
      /// </summary>
      Fatal = 5
   }
}
