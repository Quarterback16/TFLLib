using NLog;
using System;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using TFLLib.Caching;

namespace TFLLib
{
	/// <summary>
	/// Summary description for DataLibrarian.
	/// This class is an example of "Table Data Gateway"?
	/// Fowler 2003
	/// Returns data using generic datasets
	/// </summary>
	public class DataLibrarian : ITflDataLibrarian
	{
		#region Properties

		public string NflConnectionName { get; set; }
		public string NflConnectionString { get; set; }
		public string ConStr;
		//  The three directories where DBF data can be found
		public OleDbConnection OleDbConn;
		public OleDbConnection OleDbConnTycoon;
		public OleDbConnection OleDbConnControl;

		public Logger Logger { get; set; }

		public bool UseCache { get; set; }

		private readonly ICacheRepository cache;

		#endregion Properties

		#region Constructors

		public DataLibrarian(
            string nflConnection,
            string tflConnection,
            string ctlConnection )
		{
			try
			{
				Logger = LogManager.GetCurrentClassLogger();
				// Connect to a database
				OleDbConn = new OleDbConnection( nflConnection );
				//  This is the default (ie dont return deleted records)
				OleDbConn.Open();
				var cmd = new OleDbCommand( "SET DELETED ON", OleDbConn );
				cmd.ExecuteNonQuery();
				OleDbConnTycoon = new OleDbConnection( tflConnection );
				OleDbConnControl = new OleDbConnection( ctlConnection );
				NflConnectionString = nflConnection;

                Logger.Trace(message: $"Data Librarian connected to {nflConnection}");

                try
                {
                    Logger.Trace(message: "Turning Cache off");
                    UseCache = false;
                    //cache = new RedisCacheRepository();
                }
                catch (Exception)
                {
                    Logger.Trace(message: "Turning Cache off");
                    UseCache = false;
                }
			}
			catch ( Exception ex )
			{
				Logger.ErrorException( $"Connection failed: {nflConnection}>>{ex.Message}", ex );
				throw;
			}
		}

		#endregion Constructors

		#region TFL_CTL

		public string GetCurrentWeek()
		{
			const string commandStr = "SELECT * FROM TFL_CTL ";

			var ds = GetNflDataSet( "tfl_ctl", commandStr );
			var week = "??";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				week = ds.Tables[ 0 ].Rows[ 0 ][ "Week" ].ToString();
			return week;
		}

		public string GetCurrentSeason()
		{
			const string commandStr = "SELECT * FROM TFL_CTL ";

			var ds = GetNflDataSet( "tfl_ctl", commandStr );
			var season = "????";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				season = ds.Tables[ 0 ].Rows[ 0 ][ "season" ].ToString();
			return season;
		}

		#endregion TFL_CTL

		#region LINEUP

		public DataSet PositionsUsed( string teamCode, string season )
		{
			var previousSeason = ( Int32.Parse( season ) - 1 );
			var commandStr = string.Format(
			   "SELECT DISTINCT POS, GAMECODE FROM LINEUP where (SEASON='{0}' or SEASON='{2}') and TEAMCODE='{1}' and START",
			   season, teamCode, previousSeason );
			return GetNflDataSet( "lineup", commandStr );
		}

		public string GetStarter( string teamCode, string season, int week, string pos, int slot )
		{
			var starter = "???";
			var plyrCnt = 0;
			var commandStr = string.Format(
			   "SELECT PLAYERID FROM LINEUP where TEAMCODE='{0}' and POS='{3}' and WEEK='{1:0#}' and SEASON='{2}' and START",
			   teamCode, week, season, pos );

			var ds = GetNflDataSet( "lineup", commandStr );
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
			{
				var dt = ds.Tables[ "lineup" ];
				foreach ( DataRow dr in dt.Rows )
				{
					plyrCnt++;
					if ( plyrCnt != slot ) continue;
					starter = dr[ "PLAYERID" ].ToString();
					break;
				}
			}
			return starter;
		}

		public DataSet GetLineup( string teamCode, string season, int week )
		{
			var keyValue = $"{"GetLineup-DataSet"}:{season}:{week}:{teamCode}";
			var commandStr 
				= $"SELECT * FROM LINEUP where TEAMCODE='{teamCode}' and WEEK='{week:0#}' and SEASON='{season}'";
			var ds = CacheCommand( keyValue, commandStr, "lineup", "GetLineup" );
			return ds;
		}

		public DataSet GetStarts( string playerCode, string season )
		{
			playerCode = FixSingleQuotes( playerCode );
			var keyValue = $"{"GetStarts-DataSet"}:{season}:{playerCode}";

			string commandStr = string.Format(
			"SELECT DISTINCT * FROM LINEUP where PLAYERID='{0}' and SEASON='{1}' and START",
			playerCode, season );

			var ds = CacheCommand( keyValue, commandStr, dsName: "lineup", caller: "GetStarts" );
			return ds;
		}

		#endregion LINEUP

		#region TEAM

		public DataRow TeamDataFor( string teamCode, string season )
		{
			var keyValue = $"{"TeamDataFor-DataSet"}:{teamCode}:{season}";
			var commandStr = string.Format( "SELECT * FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );
			var ds = CacheCommand( keyValue, commandStr, "team", "TeamDataFor" );
			return ds.Tables[ 0 ].Rows.Count > 0 ? ds.Tables[ 0 ].Rows[ 0 ] : null;
		}

		public string TeamFor( string teamCode, string season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "TeamFor-DataSet",
			   teamCode, season );
			var commandStr = string.Format(
			   "SELECT CITY, TEAMNAME FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "team", "TeamFor" );

			var team = "???";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				team = ds.Tables[ 0 ].Rows[ 0 ][ "City" ].ToString().Trim() + " " + ds.Tables[ 0 ].Rows[ 0 ][ "Teamname" ].ToString().Trim();
			return team;
		}

		public string GetDivFor( string teamCode, string season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "GetDivFor-DataSet",
			   teamCode, season );

			var commandStr = string.Format(
			   "SELECT DIVISION FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "team", "GetDivFor" );

			var div = "?";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				div = ds.Tables[ 0 ].Rows[ 0 ][ "DIVISION" ].ToString().Trim();
			return div;
		}

		public string GetRatingsFor( string teamCode, string season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", 
				"GetRatingsFor-DataSet", teamCode, season );
			var commandStr = string.Format(
			   "SELECT RATE FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "team", "GetRatingsFor" );

			var rate = "?";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				rate = ds.Tables[ 0 ].Rows[ 0 ][ "RATE" ].ToString().Trim();
			return rate;
		}

		/// <summary>
		///   Returns teams in XML
		/// </summary>
		/// <param name="season"></param>
		/// <returns></returns>
		public string Teams( string season )
		{
			var keyValue = string.Format( "{0}:{1}", "Teams-DataSet", season );

			var commandStr = string.Format(
			   "SELECT CITY, TEAMNAME FROM TEAM where SEASON='{0}'", season );

			var ds = CacheCommand( keyValue, commandStr, "team", "Teams" );

			var teamsXml = ds.GetXml();
			return teamsXml;
		}

		public DataSet TeamRecord( string teamCode, string season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "TeamDataFor-DataSet",
			   teamCode, season );

			var commandStr = string.Format(
			   "SELECT * FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "team", "TeamRecord" );

			return ds;
		}

		public int GetTeamStat( string teamCode, string statName, string season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "GetTeamStat-DataSet",
						teamCode, statName, season );

			var stat = 0;
			var commandStr = string.Format(
			   "SELECT * FROM TEAM where SEASON='{0}' and TEAMID='{1}'",
			   season, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "team", "GetTeamStat" );
			var dt = ds.Tables[ 0 ];
			if ( dt.Rows.Count == 1 )
			{
				var dr = dt.Rows[ 0 ];
				stat = Int32.Parse( dr[ statName ].ToString() );
			}
			return stat;
		}

		/// <summary>
		///   Returns teams in a DataSet
		/// </summary>
		/// <param name="season"></param>
		/// <returns></returns>
		public DataSet TeamsDs( string season )
		{
			var commandStr = string.Format(
			   "SELECT CITY, TEAMNAME, TEAMID, WINS FROM TEAM where SEASON='{0}'", season );
			return GetNflDataSet( "team", commandStr );
		}

		/// <summary>
		///   Returns teams in a Dataset for an optional Division
		/// </summary>
		/// <param name="season"></param>
		/// <param name="div"></param>
		/// <returns></returns>
		public DataSet GetTeams( string season, [Optional] string div )
		{
			var keyValue = $"{"GetTeams-DataSet"}:{season}:{div}";

			var commandStr = "SELECT * FROM TEAM where SEASON ='" + season + "'";

			if ( !string.IsNullOrEmpty( div ) )
				commandStr += " and DIVISION ='" + div + "'";

			commandStr += " order by CLIP desc";

			var ds = CacheCommand( keyValue, commandStr, dsName:"team", caller: "GetTeams" );
			return ds;
		}

		public void UpdatePlayoff( string season, string teamCode, bool playoff )
		{
			var playoffChar = " ";
			if ( playoff ) playoffChar = "Y";
			var formatStr = "UPDATE TEAM SET PLAYOFFS = '{0}' ";
			formatStr += " WHERE SEASON='{1}' AND TEAMID ='{2}'";
			var commandStr = string.Format( formatStr, playoffChar, season, teamCode );
			ExecuteNflCommand( commandStr );
		}

		public void UpdateRatings( string season, string teamCode, string ratings )
		{
			var formatStr = "UPDATE TEAM SET RATE = '{0}' ";
			formatStr += " WHERE SEASON='{1}' AND TEAMID ='{2}'";
			var commandStr = string.Format( formatStr, ratings, season, teamCode );
			ExecuteNflCommand( commandStr );
		}

		#endregion TEAM

		#region SCORE

		public DataSet ScoresDs( string season, [Optional] string week, [Optional] string game )
		{
			string commandStr;

			if ( string.IsNullOrEmpty( week ) )
				commandStr = string.Format(
				   "SELECT * FROM SCORE where SEASON='{0}'", season );
			else
			{
				if ( string.IsNullOrEmpty( game ) )
					commandStr = string.Format(
					   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}'", season, week );
				else
					commandStr = string.Format(
					   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}'",
					   season, week, game );
			}

			return GetNflDataSet( "score", commandStr, "ScoresDs" );
		}

		public DataSet ScoresDs( string scoreType, string teamCode, string season, string week, string game )
		{
			var commandStr = string.Format(
					 "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and TEAM='{3}' and SCORE='{4}'",
					 season, week, game, teamCode, scoreType );

			return GetNflDataSet( "score", commandStr, "ScoresDs2" );
		}

		public int CountScoresByType( string season, string scoreType )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and SCORE='{1}'", season, scoreType );

			var ds = GetNflDataSet( "score", commandStr, "CountScoresByType" );

			return ds.Tables[ 0 ].Rows.Count;
		}

		public string YearOfLastScore( string playerId )
		{
			playerId = FixSingleQuotes( playerId );

			var yearOfLastScore = string.Empty;

			var commandStr = string.Format(
			   "SELECT * FROM SCORE where PLAYERID1 ='{0}' .or. PLAYERID2 ='{0}' ORDER BY SEASON DESC", 
			   playerId );

			var ds = GetNflDataSet( "score", commandStr, "YearOfLastScore" );
			if ( ds != null )
			{
				if ( ds.Tables[ 0 ].Rows.Count > 0 )
				{
					yearOfLastScore = ds.Tables[ 0 ].Rows[ 0 ][ "SEASON" ].ToString();
				}
			}
			return yearOfLastScore;
		}

		public DataTable ScoresDtByType( string season, string scoreType )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and SCORE='{1}'", season, scoreType );

			var ds = GetNflDataSet( "score", commandStr, "ScoresDtByType" );

			return ds.Tables[ 0 ];
		}

		/// <summary>
		///    Get a data set of scores for a particular player.
		/// </summary>
		/// <param name="scoreType">What type of score, eg FG</param>
		/// <param name="playerId">ID of the player who scored</param>
		/// <returns>DataSet</returns>
		public DataSet GetScoresFor( string scoreType, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where PLAYERID1='{1}' and SCORE='{0}'",
			   scoreType, playerId );
			return GetNflDataSet( "score", commandStr, "GetScoresFor" );
		}

		public DataSet GetSeasonScoresFor(
		   string scoreType, string season1, string season2 )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SCORE='{0}' and ( season>='{1}' and season <='{2}' )",
			   scoreType, season1, season2 );
			return GetNflDataSet( "score", commandStr, "GetSeasonScoresFor" );
		}

		public DataSet GetPlayersScoring(
		   string scoreType, string season1, string season2, string scoreSlot )
		{
			var commandStr = string.Format(
			   "SELECT DISTINCT PLAYERID{3} FROM SCORE where SCORE='{0}' and ( season>='{1}' and season <='{2}' )",
			   scoreType, season1, season2, scoreSlot );
			return GetNflDataSet( "score", commandStr, "GetPlayersScoring" );
		}

		/// <summary>
		///    Get a data set of scores for a particular team in a particular game.
		/// </summary>
		/// <param name="scoreType">What type of score, eg FG</param>
		/// <param name="teamCode">ID of the team who scored</param>
		/// <param name="season">the season</param>
		/// <param name="week">the week</param>
		/// <param name="gameCode">the game</param>
		/// <returns>DataSet</returns>
		public DataSet GetTeamScoresFor(
		   string scoreType, string teamCode, string season, string week, string gameCode )
		{
			var keyValue = $"{"GetTeamScoresFor-DataSet"}:{scoreType}:{teamCode}:{season}:{week}:{gameCode}";
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where TEAM='{1}' and SCORE='{0}' and SEASON='{2}' and WEEK='{3}' and GAMENO='{4}'",
			   scoreType, teamCode, season, week, gameCode );
			var ds = CacheCommand( keyValue, commandStr, "score", "GetTeamScoresFor" );
			return ds;
		}

		public int GetTotTeamScoresFor(
		   string scoreType, string teamCode, string season, string week, string gameCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and SCORE='{3}' and TEAM='{4}'",
			   season, week, gameCode, scoreType, teamCode );

			var ds = GetNflDataSet( "score", commandStr, "GetTotTeamScoresFor" );

			return ds.Tables[ 0 ].Rows.Count;
		}

		public DataSet GetTeamScoresFor( string scoreType, string teamCode, string season )
		{
			var keyValue = $"{"GetTeamScoresFor-DataSet"}:{teamCode}:{season}:{scoreType}";
			var commandStr = string.Format(
			"SELECT * FROM SCORE where TEAM='{1}' and SCORE='{0}' and SEASON='{2}'",
			scoreType, teamCode, season );
			var ds = CacheCommand( keyValue, commandStr, dsName: "score", caller: "GetTeamScoresFor" );
			return ds;
		}

		public DataSet GetTeamRegularSeasonScoresFor(
		   string scoreType, string teamCode, string season )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where TEAM='{1}' and SCORE='{0}' and SEASON='{2}' and WEEK < '18'",
			   scoreType, teamCode, season );
			return GetNflDataSet( "score", commandStr, "GetTeamRegularSeasonScoresFor" );
		}

		public DataSet GetTeamDefensiveScoresFor(
		   string teamCode, string season, string week, string gameCode )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}", "GetTeamDefensiveScoresFor-DataSet",
						  teamCode, season, week, gameCode );
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and TEAM='{3}' and (SCORE='F' or SCORE='I' or SCORE='K' or SCORE = 'S' or SCORE = 'T')",
			   season, week, gameCode, teamCode );
			var ds = CacheCommand( keyValue, commandStr, "score", "ResultFor" );
			return ds;
		}

		/// <summary>
		///   Looking for overtime games
		/// </summary>
		/// <param name="season"></param>
		/// <param name="week"></param>
		/// <param name="gameCode"></param>
		/// <returns></returns>
		public DataSet GetOvertimeScoresFor( string season, string week, string gameCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where substr( WHEN, 1, 1 ) = '5' and SEASON='{0}' and WEEK='{1}' and GAMENO='{2}'",
			   season, week, gameCode );
			return GetNflDataSet( "score", commandStr, "GetOvertimeScoresFor" );
		}

		public DataSet GetScoresForWeek( string scoreType, string playerId, string season, int week )
		{
			playerId = FixSingleQuotes( playerId );
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where PLAYERID1='{1}' and SCORE='{0}' and WEEK='{2:0#}' and SEASON='{3}'",
			   scoreType, playerId, week, season );
			return GetNflDataSet( "score", commandStr, "GetScoresForWeek" );
		}

		public DataSet GetTDcForWeek( string playerId, string season, int week )
		{
			playerId = FixSingleQuotes( playerId );

			var commandStr = string.Format(
			   "SELECT * FROM SCORE where PLAYERID1='{1}' and SCORE='{0}' and WEEK='{2:0#}' and SEASON='{3}'",
			   "P", playerId, week, season );
			return GetNflDataSet( "score", commandStr, "GetTDcForWeek" );
		}

		public DataSet GetTDpForWeek( string playerId, string season, int week )
		{
			playerId = FixSingleQuotes( playerId );

			var commandStr = string.Format(
			   "SELECT * FROM SCORE where PLAYERID2='{1}' and SCORE='{0}' and WEEK='{2:0#}' and SEASON='{3}'",
			   "P", playerId, week, season );
			return GetNflDataSet( "score", commandStr, "GetTDpForWeek" );
		}

		public DataSet GetScoresForWeeks( string scoreType, string playerId, string season, int fromWeek, int toWeek, string id )
		{
			playerId = FixSingleQuotes( playerId );
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}:{5}:{6}",
				  "GetScoresForWeeks-DataSet", scoreType, playerId, season, fromWeek, toWeek, id );
			var commandStr = string.Format(
			"SELECT * FROM SCORE where PLAYERID{5}=\"{1}\" and SCORE='{0}' and WEEK>='{4:0#}'  and WEEK<='{2:0#}' and SEASON='{3}'",
			scoreType, playerId, fromWeek, season, toWeek, id );
			commandStr += " order by WEEK ASC";
			var ds = CacheCommand( keyValue, commandStr, dsName: "score", caller: "GetScoresForWeeks" );
			return ds;
		}

		public DataSet PlayerScoresDs( string season, string week, string playerId )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{1}' and (PLAYERID1=\"{2}\" or PLAYERID2=\"{2}\")",
			   season, week, playerId );
			return GetNflDataSet( "score", commandStr, "PlayerScoresDs" );
		}

		public DataSet PenaltyScores( string season, string week, string teamCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{2}' and TEAM='{1}' and SCORE='R' and DISTANCE=1",
			   season, teamCode, week );
			return GetNflDataSet( "score", commandStr, "PenaltyScores" );
		}

		public decimal TeamScores(
		   string scoreCode, string season, string week, string game, string teamCode )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}:{5}", "TeamDataFor-DataSet",
				scoreCode, season, week, game, teamCode );

			var commandStr = string.Format(
			   "SELECT * FROM SCORE where SEASON='{0}' and WEEK='{3}' and GAMENO='{1}' and TEAM='{2}' and SCORE='{4}'",
			   season, game, teamCode, week, scoreCode );

			var ds = CacheCommand( keyValue, commandStr, "score", "TeamScores" );

			var scores = ds.Tables[ 0 ].Rows.Count;
			return Decimal.Parse( scores.ToString( CultureInfo.InvariantCulture ) );
		}

		public bool AnyScoresForGame( string playerId, string season, int week, string gameCode )
		{
			playerId = FixSingleQuotes( playerId );

			var commandStr = string.Format(
			   "SELECT * FROM SCORE where ( PLAYERID1=\"{0}\" or PLAYERID2=\"{0}\" ) and WEEK='{2:0#}' and SEASON='{1}' and GAMENO='{3}'",
			   playerId, season, week, gameCode );

			var ds = GetNflDataSet( "score", commandStr, "AnyScoresForGame" );
			var dt = ds.Tables[ "score" ];
			return ( dt.Rows.Count > 0 );
		}

		#endregion SCORE

		#region STAT

		public DataSet PlayerStatsDs( string season, string week, [Optional] string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "PlayerStatsDs-DataSet",
			   season, week, playerId );

			var commandStr = string.Format( string.IsNullOrEmpty( playerId )
			? "SELECT * FROM STAT where SEASON='{0}' and WEEK='{1}'"
			: "SELECT * FROM STAT where SEASON='{0}' and WEEK='{1}' and PLAYERID=\"{2}\"", season, week, playerId );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "PlayerStatsDs" );
			return ds;
		}

		public int GetStat( string statCode, string season, string week, string game )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}", "GetStat-DataSet",
								  season, week, statCode, game );

			var commandStr = string.Format(
			"SELECT sum(QTY) as sum FROM STAT where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and STAT='{3}'",
			season, week, game, statCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "GetStat" );
			var dr = ds.Tables[ 0 ].Rows[ 0 ];
			var nSumStat = ( int ) dr[ "sum" ];
			return nSumStat;
		}

		public DataSet GetStats( string statCode, string teamCode, string season, string week, string game )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}:{5}", "GetStats-DataSet",
			   statCode, teamCode, season, week, game );

			var commandStr = string.Format(
			"SELECT * FROM STAT where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and STAT='{3}' and TEAMID='{4}'",
			season, week, game, statCode, teamCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "GetStats" );
			return ds;
		}

		public decimal GetTotStats(
		   string teamCode, string statCode, string season, string week, string game )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}:{5}", "GetTotStats-DataSet",
			   teamCode, statCode, season, week, game );
			var nSumStat = 0.0M;

			var commandStr = string.Format(
			"SELECT sum(QTY) as sum FROM STAT where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' and STAT='{3}' and TEAMID='{4}'",
			season, week, game, statCode, teamCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "GetTotStats" );
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
			{
				var dr = ds.Tables[ 0 ].Rows[ 0 ];
				nSumStat = ( decimal ) dr[ "sum" ];
			}
			return nSumStat;
		}

		public decimal TeamStats(
		   string statCode, string season, string week, string game, string teamCode )
		{
			var keyValue = $"{"TeamStats - DataSet"}:{season}:{week}:{statCode}:{game}:{teamCode}";
			var stats = 0.0M;
			var commandStr = string.Format(
				"SELECT * FROM STAT where SEASON='{0}' and WEEK='{3}' and GAMENO='{1}' and TEAMID='{2}' and STAT='{4}'",
				season, game, teamCode, week, statCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "TeamStats" );
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
			{
				var dt = ds.Tables[ "stat" ];
				stats += ( from DataRow dr in dt.Rows
						   select dr[ "QTY" ].ToString()
							 into thisGame
						   select Decimal.Parse( thisGame ) ).Sum();
			}
			return stats;
		}

		public string PlayerStats(
		   string statCode, string season, string week, string game, string teamCode )
		{
			var keyValue = $"{"PlayerStatsDs-DataSet"}:{season}:{week}:{statCode}:{game}:{teamCode}";
			var thisGame = "";
			var commandStr = string.Format(
			"SELECT * FROM STAT where SEASON='{0}' and WEEK='{3}' and GAMENO='{1}' and TEAMID='{2}' and STAT='{4}'",
			season, game, teamCode, week, statCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "PlayerStats" );

			if ( ds.Tables[ 0 ].Rows.Count > 0 )
			{
				var dt = ds.Tables[ "stat" ];
				thisGame = dt.Rows.Cast<DataRow>().Aggregate(
				   thisGame, ( current, dr ) => current + string.Format( "{0} {1}, ",
					  GetPlayerName( dr[ "PLAYERID" ].ToString() ), dr[ "QTY" ] ) );
				if ( thisGame.Length > 0 ) thisGame = thisGame.Substring( 0, thisGame.Length - 2 );
			}
			return thisGame;
		}

		public string PlayerStats(
		   string statCode, string season, string week, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			var thisGame = "";
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}", "PlayerStats-DataSet",
			   season, week, playerId, statCode );

			var commandStr = string.Format(
			"SELECT * FROM STAT where SEASON='{0}' and WEEK='{1}' and PLAYERID='{2}' and STAT='{3}'",
			season, week, playerId, statCode );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "PlayerStats" );

			if ( ds.Tables[ 0 ].Rows.Count > 0 )
			{
				var dt = ds.Tables[ "stat" ];
				thisGame = dt.Rows.Cast<DataRow>().Aggregate(
				   thisGame, ( current, dr ) => current + string.Format( "{0}",
					   dr[ "QTY" ] ) );
				if ( thisGame.Length > 0 ) thisGame = thisGame.Substring( 0, thisGame.Length - 2 );
			}
			return thisGame;
		}

		public DataSet GetStatsForWeeks(
		   string playerId, string season, int fromWeek, int toWeek, string statType )
		{
			playerId = FixSingleQuotes( playerId );

			var keyValue = $"{"GetStatsForWeeks-DataSet"}:{playerId}:{season}:{fromWeek}:{toWeek}:{statType}";

			var commandStr = string.Format(
				"SELECT * FROM STAT where PLAYERID=\"{0}\" and WEEK>='{3:0#}'  and WEEK<='{1:0#}' and SEASON='{2}' and STAT='{4}'",
				playerId, fromWeek, season, toWeek, statType );

			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "GetStatsForWeeks" );

			return ds;
		}

		public bool AnyStatsForGame( string playerId, string season, int week, string gameCode )
		{
			playerId = FixSingleQuotes( playerId );
			var keyValue = $"{"AnyStatsForGame-DataSet"}:{season}:{week:00}:{playerId}:{gameCode}";
			var commandStr = string.Format(
				"SELECT * FROM STAT where PLAYERID=\"{0}\" and WEEK='{2:0#}'  and SEASON='{1}' and GAMENO='{3}'",
				playerId, season, week, gameCode );
			var ds = CacheCommand( keyValue, commandStr, dsName: "stat", caller: "AnyStatsForGame" );
			var dt = ds.Tables[ "stat" ];
			return ( dt.Rows.Count > 0 );
		}

		public string YearOfLastStat( string playerId )
		{
			playerId = FixSingleQuotes( playerId );

			var yearOfLastStat = string.Empty;

			var commandStr = string.Format(
			   "SELECT * FROM STAT where PLAYERID ='{0}' ORDER BY SEASON DESC",
			   playerId );

			var ds = GetNflDataSet( "STAT", commandStr, "YearOfLastStat" );
			if ( ds != null )
			{
				if ( ds.Tables[ 0 ].Rows.Count > 0 )
				{
					yearOfLastStat = ds.Tables[ 0 ].Rows[ 0 ][ "SEASON" ].ToString();
				}
			}

			return yearOfLastStat;
		}

		#endregion STAT

		#region PLAYER

		public string NextId( string firstName, string surname )
		{
			var last4 = surname.Substring( 0, 4 );
			var first2 = firstName.Substring( 0, 2 );
			var commandStr = string.Format(
			   "SELECT * FROM PLAYER where SUBSTR( SURNAME, 1, 4)='{0}' and SUBSTR( FIRSTNAME, 1, 2 )='{1}'",
			   last4, first2 );
			var ds = GetNflDataSet( "player", commandStr );

			return string.Format( "{0}{1}{2:0#}", last4.ToUpper(), first2.ToUpper(), ds.Tables[ 0 ].Rows.Count + 1 );
		}

		public DataSet GetPlayer( string firstName, string surname )
		{
			if ( firstName.Contains( '\'' ) )
				firstName = firstName.Replace( "'", "''" );
			if ( surname.Contains( '\'' ) )
				surname = surname.Replace( "'", "''" );
			var keyValue = string.Format( "{0}:{1}:{2}", "GetPlayer-DataSet", firstName, surname );
			var commandStr = string.Format(
				"SELECT * FROM PLAYER where SURNAME='{0}' and FIRSTNAME='{1}' order by DOB desc", surname, firstName );
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetPlayer" );
			return ds;
		}

		public DataSet GetReturners()
		{
			var keyValue = string.Format( "{0}", "GetReturners-DataSet" );
			const string commandStr = "SELECT * FROM PLAYER where CURRTEAM<>'??' and CURRTEAM<>'  ' " +
									   "and ( POSDESC like '%KR%' or POSDESC like '%PR%' ) and ROLE <> ' '";
			var ds = CacheCommand( keyValue, commandStr, dsName: "returners", caller: "GetReturners" );
			return ds;
		}

		public DataSet GetCurrentScoring( string categoryIn )
		{
			var keyValue = string.Format( "{0}:{1}", "GetCurrentScoring-DataSet",
			   categoryIn );
			string commandStr = string.Format(
			"SELECT CURSCORES, * FROM PLAYER where CATEGORY='{0}' ", categoryIn );
			commandStr += " and CURSCORES > 0 ORDER BY 1 DESC";
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetCurrentScoring" );
			return ds;
		}

		public DataSet GetScoring( string categoryIn, bool currentOnly, int season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3:0000}", "GetScoring-DataSet",
			   categoryIn, currentOnly, season );

			string commandStr = string.Format(
				"SELECT (scores/({0}-rookieyr)) as 'Output', * FROM PLAYER where CATEGORY='{1}' ", season + 1, categoryIn );
			if ( currentOnly ) commandStr += " and CURRTEAM<>'??' and CURRTEAM<>'  ' ";
			commandStr += " and SCORES > 10 ORDER BY 1 DESC";
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetScoring" );
			return ds;
		}

		public DataSet GetFreeAgents( string categoryIn, bool currentOnly, int season )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "GetFreeAgents-DataSet",
			   categoryIn, currentOnly, season.ToString() );
			var commandStr = string.Format(
			"SELECT (scores/({0}-rookieyr)) as 'Output', * FROM PLAYER where CATEGORY='{1}' ", season + 1, categoryIn );

			if ( currentOnly ) commandStr += " and Role = 'S' and FTEAM = and CURRTEAM<>'??' and CURRTEAM<>'  ' ";
			commandStr += " and SCORES > 10 ORDER BY 1 DESC";
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetFreeAgents" );
			return ds;
		}

		public DataSet GetPlayer(
		   string teamCode, string strCat, string strRole, string strPos )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}:{4}", "GetPlayer4-DataSet",
										  teamCode, strCat, strRole, strPos );
			if ( !cache.TryGet( keyValue, out DataSet ds ) )
			{
				string commandStr;

				if ( strCat == "*" )
				{
					commandStr = "SELECT * FROM PLAYER where CURRTEAM ='" + teamCode + "' ";
					if ( strRole != "*" || !string.IsNullOrEmpty( strRole ) )
						commandStr += " and ROLE ='" + strRole + "'";
				}
				else
				{
					if ( strPos.Equals( "RB" ) )
					{
						commandStr =
						   "SELECT * FROM PLAYER where CURRTEAM ='" + teamCode + "'" + " and " +
						   "CATEGORY ='" + strCat + "'"
						   + " and ( POSDESC like '%RB%' or POSDESC like '%HB%' )";
						if ( strRole != "*" && !string.IsNullOrEmpty( strRole ) )
							commandStr += " and ROLE ='" + strRole + "'";
					}
					else
					{
						if ( strRole == String.Empty )
							commandStr =
							   "SELECT * FROM PLAYER where CURRTEAM ='" + teamCode + "'" + " and " +
							   "CATEGORY ='" + strCat + "'";
						else
							commandStr =
							   "SELECT * FROM PLAYER where CURRTEAM ='" + teamCode + "'" + " and " +
							   "CATEGORY ='" + strCat + "'" + " and " +
							   "ROLE ='" + strRole + "'";
					}
				}
				if ( ( strPos != "*" ) && strPos != "RB" )
					commandStr += " and POSDESC like '%" + strPos.Trim() + "%'";

				commandStr += " order by CATEGORY";
				ds = GetNflDataSet( "player", commandStr, "GetPlayer" );
				Logger.Trace( $"{commandStr} begets {ds.Tables[ 0 ].Rows.Count} players" );
				cache.Set( keyValue, ds, new TimeSpan( hours: 2, minutes: 0, seconds: 0 ) );
			}
			else
				LogCacheHit( keyValue );
			return ds;
		}

		public DataSet GetPlayer( string teamCode, string strRole, string strPos )
		{
			return GetPlayer( teamCode, "*", strRole, strPos );
		}

		public DataSet GetTeamPlayers( string teamCode, string strCat )
		{
			var keyValue = $"{"GetTeamPlayers-DataSet"}:{teamCode}:{strCat}";

			var commandStr = "SELECT * FROM PLAYER where CURRTEAM ='" 
				+ teamCode + "' AND CATEGORY='" + strCat + "'";
			commandStr += " order by CATEGORY";
			var ds = CacheCommand( keyValue, commandStr, "player", "GetTeamPlayers" );
			return ds;
		}

		public DataSet GetPlayer( string playerCode )
		{
			playerCode = FixSingleQuotes( playerCode );
			var keyValue = $"{"GetPlayer-DataSet"}:{playerCode}";
			var commandStr = $"SELECT * FROM PLAYER where PLAYERID =\"{playerCode}\"";
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetPlayer3",
			   timeSpan: TimeSpan.FromHours( 1 ) );
			return ds;
		}

		public string GetPlayerName( string playerCode )
		{
			playerCode = FixSingleQuotes( playerCode );
			var keyValue = $"{"GetPlayerName-DataSet"}:{playerCode}";
			var commandStr = string.Format( "SELECT * FROM PLAYER where PLAYERID =\"{0}\"", playerCode );
			var ds = CacheCommand( keyValue, commandStr, dsName: "player", caller: "GetPlayerName" );
			var dt = ds.Tables[ "player" ];
			var dr = dt.Rows[ 0 ];
			return $"{dr[ "FIRSTNAME" ].ToString().Substring( 0, 1 )} {dr[ "SURNAME" ].ToString().Trim()}";
		}

		/// <summary>
		///   Returns a collection of players
		/// </summary>
		/// <param name="strCat">Pass * for all categories</param>
		/// <param name="sPos"></param>
		/// <param name="role"></param>
		/// <returns></returns>
		public DataSet GetPlayers( string strCat, [Optional] string sPos, [Optional] string role, [Optional] string rookieYr )
		{
			string commandStr;

			if ( string.IsNullOrEmpty( role ) )
			{
				if ( strCat == "*" )
					commandStr = "SELECT * FROM PLAYER where CURRTEAM != '??' ";
				else
					commandStr =
					   "SELECT * FROM PLAYER where  CURRTEAM != '??'and " +
					   "CATEGORY ='" + strCat + "'";

				commandStr += " and (ROLE='S'.or.ROLE='B')";
			}
			else
			{
				if ( strCat == "*" )
					commandStr = "SELECT * FROM PLAYER where CURRTEAM != '??'  ";
				else
					commandStr =
					   "SELECT * FROM PLAYER where  CURRTEAM != '??'  and " +
					   "CATEGORY ='" + strCat + "'";
				if ( role != "*" )
				{
					commandStr += " and ROLE='" + role + "' ";
				}
			}
			if ( !string.IsNullOrEmpty( rookieYr ) )
			{
				commandStr += string.Format( " and ( ROOKIEYR = {0})", rookieYr );
			}

			var ds = GetNflDataSet( "player", commandStr, "GetPlayers4" );

			//  optionally filter on position
			if ( !string.IsNullOrEmpty( sPos ) )
			{
				var dt = ds.Tables[ 0 ];
				foreach ( DataRow dr in dt.Rows )
				{
					if ( dr[ "POSDESC" ].ToString().IndexOf( sPos ) != -1 ) continue;
					//  did not find the pos in players description
					if ( strCat.Equals( "2" ) )
					{
						if ( sPos.Equals( "RB" ) && dr[ "POSDESC" ].ToString().IndexOf( "HB" ) == -1 ) //  HB is equivalent to RB
							dr.Delete(); //  wrong pos
					}
					else if ( strCat.Equals( "4" ) )
					{
						//  dont delete
					}
					else
						dr.Delete(); //  wrong pos
				}
			}

			return ds;
		}

		public DataSet GetCurrentPlayers( string teamCode, [Optional] string strCat, [Optional] string sPos, [Optional] string role )
		{
			string commandStr;

			if ( string.IsNullOrEmpty( role ) )
			{
				if ( string.IsNullOrEmpty( strCat ) || strCat == "*" )
					commandStr = "SELECT * FROM PLAYER where CURRTEAM == '" + teamCode + "'";
				else
					commandStr =
					   "SELECT * FROM PLAYER where  CURRTEAM == '" + teamCode + "'" +
					   "CATEGORY ='" + strCat + "'";
			}
			else
			{
				if ( strCat == "*" )
					commandStr = "SELECT * FROM PLAYER where CURRTEAM == '" + teamCode + "' and ROLE='" + role + "' ";
				else
					commandStr =
					   "SELECT * FROM PLAYER where CURRTEAM == '" + teamCode + "' and ROLE='" + role + "' and " +
					   "CATEGORY ='" + strCat + "'";
			}

			var ds = GetNflDataSet( "player", commandStr, "GetCurrentPlayers" );

			//  optionally filter on position
			if ( !string.IsNullOrEmpty( sPos ) )
			{
				var dt = ds.Tables[ 0 ];
				foreach ( DataRow dr in dt.Rows )
				{
					if ( dr[ "POSDESC" ].ToString().IndexOf( sPos ) != -1 ) continue;
					//  did not find the pos in players description
					if ( strCat.Equals( "2" ) )
					{
						if ( sPos.Equals( "RB" ) && dr[ "POSDESC" ].ToString().IndexOf( "HB" ) == -1 ) //  HB is equivalent to RB
							dr.Delete(); //  wrong pos
					}
					else if ( strCat.Equals( "4" ) )
					{
						//  dont delete
					}
					else
						dr.Delete(); //  wrong pos
				}
			}

			return ds;
		}

		public DataSet GetOffensivePlayers( string strCats )
		{
			var commandStr =
			   "SELECT * FROM PLAYER where  CURRTEAM != '??' and ROLE='S' and " +
			   "CURRTEAM != '??' and OCCURS( player.CATEGORY, '" + strCats + "' ) > 0";

			var ds = GetNflDataSet( "player", commandStr, "GetOffensivePlayers" );

			return ds;
		}

		public DataSet GetPlayersByRole( string strCat, [Optional] string sRole, [Optional] string sPos )
		{
			string commandStr;

			if ( strCat == "*" )
				commandStr = "SELECT * FROM PLAYER where CURRTEAM != '??'";
			else
				commandStr =
				   "SELECT * FROM PLAYER where  CURRTEAM != '??' and " +
				   "CATEGORY ='" + strCat + "'";

			if ( !sRole.Equals( "*" ) )
				commandStr += " and ROLE='" + sRole + "' ";

			var ds = GetNflDataSet( "player", commandStr, "GetPlayersByRole" );

			//  optionally filter on position
			if ( !string.IsNullOrEmpty( sPos ) )
			{
				var dt = ds.Tables[ 0 ];
				foreach ( DataRow dr in dt.Rows )
				{
					if ( dr[ "POSDESC" ].ToString().IndexOf( sPos ) != -1 ) continue;
					//  did not find the pos in players description
					if ( strCat.Equals( "2" ) )
					{
						if ( sPos.Equals( "RB" ) && dr[ "POSDESC" ].ToString().IndexOf( "HB" ) == -1 ) //  HB is equivalent to RB
							dr.Delete(); //  wrong pos
					}
					else
						dr.Delete(); //  wrong pos
				}
			}

			return ds;
		}

		public DataTable GetDistinctPositions()
		{
			const string commandStr = "SELECT DISTINCT POSDESC FROM PLAYER";

			var ds = GetNflDataSet( "player", commandStr, "GetDistinctPositions" );
			return ds.Tables[ 0 ];
		}

		public DataTable GetDistinctColleges()
		{
			const string commandStr = "SELECT DISTINCT COLLEGE FROM PLAYER";
			var ds = GetNflDataSet( "player", commandStr, "GetDistinctColleges" );
			return ds.Tables[ 0 ];
		}

		public bool PlayerExists( string firstname, string surname, string college, [Optional] string dob )
		{
			string commandStr;
			string formatStr;

			if ( string.IsNullOrEmpty( dob ) )
			{
				formatStr = "SELECT * FROM PLAYER WHERE FIRSTNAME='{0}' and SURNAME='{1}' and COLLEGE='{2}'";
				commandStr = string.Format( formatStr, firstname, surname, college );
			}
			else
			{
				formatStr = "SELECT * FROM PLAYER WHERE FIRSTNAME='{0}' and SURNAME='{1}' and COLLEGE='{2}' AND DTOC(DOB)='{3}'";
				commandStr = string.Format( formatStr, firstname, surname, college, dob );
			}
			var ds = GetNflDataSet( "player", commandStr, "PlayerExists" );

			return ( ds.Tables[ 0 ].Rows.Count > 0 );
		}

		public void ResetProjections()
		{
			string commandStr = "UPDATE PLAYER SET PROJECTED = 0";
			ExecuteNflCommand( commandStr );
		}

		#endregion PLAYER

		#region COMP (TYCOON DB)

		public string GetStatus( string playerCode, string compCode, string season )
		{
			var status = "**";
			var commandStr =
			   "select * from COMP where SEASON='" + season + "'" + " and " +
			   "LEAGUEID='" + compCode + "'";
			var da = new OleDbDataAdapter( commandStr, OleDbConnTycoon );
			var ds = new DataSet();
			da.Fill( ds, "comp" );
			var dt = ds.Tables[ "comp" ];
			foreach ( var dr in dt.Rows.Cast<DataRow>()
			   .Where( dr => ( dr[ "playera" ].ToString() == playerCode ) | ( dr[ "playerb" ].ToString() == playerCode ) |
						 ( dr[ "playerc" ].ToString() == playerCode ) | ( dr[ "playerd" ].ToString() == playerCode ) |
						 ( dr[ "playere" ].ToString() == playerCode ) | ( dr[ "playerf" ].ToString() == playerCode ) |
						 ( dr[ "playerg" ].ToString() == playerCode ) | ( dr[ "playerh" ].ToString() == playerCode ) |
						 ( dr[ "playeri" ].ToString() == playerCode ) | ( dr[ "playerj" ].ToString() == playerCode ) |
						 ( dr[ "playerk" ].ToString() == playerCode ) | ( dr[ "playerl" ].ToString() == playerCode ) |
						 ( dr[ "playerm" ].ToString() == playerCode ) | ( dr[ "playern" ].ToString() == playerCode ) |
						 ( dr[ "playero" ].ToString() == playerCode ) | ( dr[ "playerp" ].ToString() == playerCode ) |
						 ( dr[ "playerq" ].ToString() == playerCode ) | ( dr[ "playerr" ].ToString() == playerCode ) |
						 ( dr[ "players" ].ToString() == playerCode ) | ( dr[ "playert" ].ToString() == playerCode ) |
						 ( dr[ "playeru" ].ToString() == playerCode ) | ( dr[ "playerv" ].ToString() == playerCode ) |
						 ( dr[ "playerw" ].ToString() == playerCode ) | ( dr[ "playerx" ].ToString() == playerCode ) |
						 ( dr[ "playery" ].ToString() == playerCode ) | ( dr[ "playerz" ].ToString() == playerCode ) ) )
			{
				status = dr[ "francode" ].ToString();
				break;
			}
			return status;
		}

		public DataSet GetFTeamsDs( string season, string compCode )
		{
			var commandStr =
			   "select * from COMP where SEASON='" + season + "'" + " and " +
			   "LEAGUEID='" + compCode + "'";

			var da = new OleDbDataAdapter( commandStr, OleDbConnTycoon );
			var ds = new DataSet();
			da.Fill( ds, "comp" );
			return ds;
		}

		public DataRow GetFTeamDr( string season, string compCode, string ownerCode )
		{
			var commandStr =
			   "select * from COMP where SEASON='" + season + "'" + " and " +
			   "LEAGUEID='" + compCode + "' and OWNERID = '" + ownerCode + "'";

			var da = new OleDbDataAdapter( commandStr, OleDbConnTycoon );
			var ds = new DataSet();
			da.Fill( ds, "comp" );
			return ds.Tables[ 0 ].Rows[ 0 ];
		}

		#endregion COMP (TYCOON DB)

		#region SEASON

		public DateTime GetSeasonStartDate( string season )
		{
			var keyValue = $"{"GetSeasonStartDate-DataSet"}:{season}";

			var commandStr = $"select * from SEASON where season='{season}'";

			var ds = CacheCommand( keyValue, commandStr, dsName: "season", caller: "GetSeasonStartDate" );

			DataTable dt = ds.Tables[ "SEASON" ];
			if ( dt.Rows.Count > 0 )
				return DateTime.Parse( dt.Rows[ 0 ][ "SUNDAY1" ].ToString() );

			return new DateTime( 1, 1, 1 );
		}

		#endregion SEASON

		#region SERVE

		public DateTime LastContract(string playerId)
		{
			var dateLastContract = new DateTime(1, 1, 1);
			var keyValue = $"{"LastContract-Date"}:{playerId}";

			var commandStr = $"select * from SERVE where PLAYERID='{playerId}'";
			commandStr += " order by FROM asc";
			var ds = CacheCommand(
				keyValue, 
				commandStr, 
				dsName: "serve", 
				caller: "LastContract");

			DataTable dt = ds.Tables["SERVE"];
			foreach (DataRow dr in dt.Rows)
			{
				if (dr.RowState == DataRowState.Deleted) continue;
				dateLastContract = DateTime.Parse(dt.Rows[0]["FROM"].ToString());
			}
			return dateLastContract;
		}

		public DataSet GetTeamPlayersByDate( 
			string teamCode,
			string catCode,
			DateTime date )
		{
			var commandStr = $"SELECT * FROM SERVE where TEAMID='{teamCode}'";
			var ds = GetNflDataSet( "serve", commandStr, "ServeDs" );
			var dt = ds.Tables[ "SERVE" ];
			var nullDate = new DateTime( 1899, 12, 30 );
			var playersFound = 0;
			//  Drop any out of range records
			foreach ( DataRow dr in dt.Rows )
			{
				if ( dr.RowState == DataRowState.Deleted ) continue;

				var dateTo = DateTime.Parse( dr[ "TO" ].ToString() );
				var dateFrom = DateTime.Parse( dr[ "FROM" ].ToString() );

				if ( ( dateFrom < date ) && ( ( dateTo < date ) && dateTo != nullDate ) )
					dr.Delete(); //  happened before
				else if (dateFrom > date )
					dr.Delete(); //  happened after
				else
				{
					if ( dateTo < date && dateTo != nullDate )
					{
						dr.Delete(); //  finished before date in question
					}
					else
						playersFound++;
				}
			}
			if ( !string.IsNullOrEmpty( catCode ) )
			{
				foreach ( DataRow dr in dt.Rows )
				{
					if ( dr.RowState == DataRowState.Deleted) continue;

					var playerId = dr[ "PLAYERID" ].ToString();
					var playerDs = GetPlayer( playerId );
					var playerDt = playerDs.Tables[ "PLAYER" ];
					foreach ( DataRow pdr in playerDt.Rows )
					{
						var cat = pdr[ "CATEGORY" ].ToString();
						if ( cat != catCode )
						{
							dr.Delete();  //  wrong category
						}
					}
				}
			}
			//  Serve ds needs to morph into a player ds
			var resultDs = new DataSet();
			var resultDt = new DataTable("player");
			var playersGot = 0;
			foreach ( DataRow filtereddr in dt.Rows )
			{
				if ( filtereddr.RowState == DataRowState.Deleted ) continue;
				var playerId = filtereddr[ "PLAYERID" ].ToString();
				var playerDs = GetPlayer( playerId );
				if ( playersGot == 0 )
				{
					resultDt = playerDs.Tables[0];
				}
				else
				{ 
					var playerDt = playerDs.Tables[ "PLAYER" ];
					foreach ( DataRow pdr in playerDt.Rows )
					{
						resultDt.ImportRow( pdr );
					}
				}
				playersGot++;
			}
			resultDt.AcceptChanges();
			var dtCopy = resultDt.Copy();
			resultDs.Tables.Add( dtCopy );
			resultDs.Tables[0].AcceptChanges();
			return resultDs;
		}

		public DataSet MovesDs( string teamCode, DateTime dFrom, DateTime dTo )
		{
			//  get all the players who ever played for the team
			var commandStr = string.Format( "SELECT * FROM SERVE where TEAMID='{0}'", teamCode );
			var ds = GetNflDataSet( "serve", commandStr, "MovesDs" );
			var dt = ds.Tables[ "SERVE" ];
			var nullDate = new DateTime( 1899, 12, 30 );
			//  Drop any out of range records
			foreach ( DataRow dr in dt.Rows )
			{
				var dateTo = DateTime.Parse( dr[ "TO" ].ToString() );
				var dateFrom = DateTime.Parse( dr[ "FROM" ].ToString() );

				if ( ( dateFrom < dFrom ) && ( ( dateTo > dTo ) || dateTo.Equals( nullDate ) ) )
					dr.Delete(); //  spanned the whole period
				else
				{
					if ( dateTo < dFrom )
					{
						if ( dateTo == nullDate )
						{
							//  still active
							if ( dateFrom < dFrom )
								dr.Delete();
							if ( dateFrom > dTo )
								dr.Delete();
						}
						else
							dr.Delete(); //  finished before period started
					}
					else
					{
						if ( dateTo == nullDate )
						{
							//  current player
							if ( dateFrom < dFrom )
								dr.Delete(); //  obtained prior to period of interest
						}
						if ( dateTo > dTo )
							dr.Delete(); //  left after the period ended - not included
					}
				}
			}
			return ds;
		}

		public string PlayedFor( string playerId, int season, int week )
		{
			playerId = FixSingleQuotes( playerId );
			var sTeam = String.Empty;
			var nullDate = new DateTime( 1, 1, 1 );
			//  determine the date of the week
			var dGame = WeekStarts( season, week );
			if ( dGame == nullDate )
				sTeam = "??";
			else
			{
				var keyValue = $"{"PlayedFor-DataSet"}:{playerId}:{season}:{week}";
				var commandStr = $"select TEAMID, FROM, TO from SERVE where PLAYERID=\"{playerId}\" order by 2 desc";

				var ds = CacheCommand( keyValue, commandStr, dsName:"serve", caller:"PlayedFor" );

				var dt = ds.Tables[ "serve" ];
				foreach ( DataRow dr in dt.Rows )
				{
					var sTo = DateTime.Parse( dr[ "TO" ].ToString() ).ToString( "dd/MM/yyyy" );
					var dTo = sTo == "30/12/1899" ? DateTime.Now : DateTime.Parse( sTo );

					if ( DateTime.Parse( dr[ "FROM" ].ToString() ) >= dGame ) continue;
					if ( dGame >= dTo ) continue;
					sTeam = dr[ "TEAMID" ].ToString();
					break;
				}
			}
			return sTeam;
		}

		/// <summary>
		///   Returns when the player was drafted by looking at his
		///   team contracts.
		/// </summary>
		/// <param name="playerCode"></param>
		/// <returns></returns>
		public string Drafted( string playerCode )
		{
			var keyValue = string.Format( "{0}:{1}", "Drafted-DataSet",
										  playerCode );
			var drafted = "??";
			var commandStr =
			   "select * from SERVE where PLAYERID=\"" + playerCode + "\" order by SERVE.FROM";

			var ds = CacheCommand( keyValue, commandStr, "SERVE", "Drafted" );

			var dt = ds.Tables[ "serve" ];
			foreach ( DataRow dr in dt.Rows )
			{
				drafted = dr[ "HOW" ].ToString();
				break;
			}
			return drafted;
		}

		/// <summary>
		/// Checks to see if the specified player has Retired.
		///
		/// </summary>
		/// <param name="playerCode">The player code.</param>
		/// <returns>The season in which the player retired.</returns>
		public string Retired( string playerCode )
		{
			var keyValue = string.Format( "{0}:{1}", "Retired-DataSet",
								 playerCode );
			var lastSeason = "";
			var commandStr =
			   "select * from SERVE where PLAYERID=\"" + playerCode + "\" order by SERVE.FROM";

			var ds = CacheCommand( keyValue, commandStr, "serve", "Retired" );
			var dt = ds.Tables[ "serve" ];
			foreach ( DataRow dr in dt.Rows )
			{
				if ( !dr[ "HOW" ].ToString().Trim().EndsWith( ".RET" ) ) continue;
				var endDate = dr[ "TO" ].ToString();
				var dEnd = DateTime.Parse( endDate );
				var lastYr = dEnd.Year;
				lastSeason = string.Format( "{0}", lastYr - 1 );
				break;
			}
			return lastSeason;
		}

		#endregion SERVE

		#region SCHED

		private DateTime WeekStarts( int season, int week )
		{
			var commandStr = string.Format(
			   "select GAMEDATE from SCHED where SEASON='{0}' and WEEK='{1:0#}'",
			   season, week );

			var ds = GetNflDataSet( "sched", commandStr );
			var dStart = ds.Tables[ 0 ].Rows.Count > 0
								 ? DateTime.Parse( ds.Tables[ 0 ].Rows[ 0 ][ "GAMEDATE" ].ToString() )
								 : new DateTime( 1, 1, 1 );
			return dStart;
		}

		public DataSet ResultFor( string teamCode, int season, int week )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "ResultFor-DataSet",
			   teamCode, season, week );
			var commandStr = string.Format(
			   "select * from SCHED where SEASON='{0}' and WEEK='{1:0#}' and (HOMETEAM='{2}' or AWAYTEAM='{2}')",
			   season, week, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "sched", "ResultFor" );
			return ds;
		}

		public DataSet GameFor( string season, string week, string gameNo )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "GameFor-DataSet",
			   season, week, gameNo );
			var commandStr = string.Format(
			"select * from SCHED where SEASON='{0}' and WEEK='{1:0#}' and GAMENO='{2}'",
			season, Int32.Parse( week ), gameNo );
			var ds = CacheCommand( keyValue, commandStr, "sched", "GameFor" );
			return ds;
		}

		public DataSet GameForTeam( string season, string week, string teamCode )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "GameForTeam-DataSet",
			   season, week, teamCode );
			var commandStr = string.Format(
			"select * from SCHED where SEASON='{0}' and WEEK='{1:0#}' and (HOMETEAM='{2}' or AWAYTEAM='{2}')",
			season, Int32.Parse( week ), teamCode );
			var ds = CacheCommand( keyValue, commandStr, "sched", "GameForTeam" );
			return ds;
		}

		public DataSet SchedDs( string season, string week )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "SchedDs-DataSet",
			   season, week );
			string commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1}'",
			   season, week );
			var ds = CacheCommand( keyValue, commandStr, "sched", "SchedDs" );
			return ds;
		}

		/// <summary>
		///    Returns a dataset containing a teams schedule for a particular season, in week order.
		/// </summary>
		/// <param name="season">eg 2005</param>
		/// <param name="teamCode">eg SF</param>
		/// <returns>DataSet "sched"</returns>
		public DataSet TeamSchedDs( string season, string teamCode )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "TeamSchedDs-DataSet",
			   season, teamCode );
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and (AWAYTEAM='{1}' or HOMETEAM='{1}') ORDER BY WEEK",
			   season, teamCode );
			var ds = CacheCommand( keyValue, commandStr, "sched", "TeamSchedDs" );
			return ds;
		}

		public DataSet GetSeason( string sTeam, string sSeason )
		{
			var keyValue = $"{"GetSeason-DataSet"}:{sTeam}:{sSeason}";
			var commandStr = $"SELECT * FROM SCHED where SEASON='{sSeason}' and (HOMETEAM='{sTeam}' or AWAYTEAM='{sTeam}') ORDER BY WEEK";
			var ds = CacheCommand( keyValue, commandStr, "sched", "GetSeason" );
			return ds;
		}

		public DataSet GetRegularSeason( string sTeam, string sSeason )
		{
			var keyValue = $"{"GetRegularSeason-DataSet"}:{sTeam}:{sSeason}";
			var commandStr = "SELECT * FROM SCHED";
			commandStr += $" where SEASON = '{sSeason}'";
			commandStr += $" and (HOMETEAM='{sTeam}' or AWAYTEAM='{sTeam}')";
			commandStr += $" and WEEK < '18'";
			commandStr += " ORDER BY WEEK";
			var ds = CacheCommand( keyValue, commandStr, "sched", "GetSeason" );
			return ds;
		}

		public string GetSuperbowlWinner( string season )
		{
			//  USED BY SUPERBOWLLetdownScheme
			string winner = "??";
			int lastSeason = Int32.Parse( season ) - 1;
			//  get the superbowl game (stored in Week 21 of the season)
			DataSet ds = GetGames( lastSeason, 21 );
			var dt = ds.Tables[ "SCHED" ];
			dt.DefaultView.Sort = "GAMENO ASC";
			foreach ( DataRow dr in dt.Rows )
				winner = ( Int32.Parse( dr[ "HOMESCORE" ].ToString() ) > Int32.Parse( dr[ "AWAYSCORE" ].ToString() ) )
							? dr[ "HOMETEAM" ].ToString()
							: dr[ "AWAYTEAM" ].ToString();
			return winner;
		}

		public DataSet GetGames( string season, string week )
		{
			return GetGames( Int32.Parse( season ), Int32.Parse( week ) );
		}

		public DataSet GetGames( int seasonIn, int weekIn )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "GetGames-DataSet",
			   seasonIn, weekIn );
			string commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1:0#}'",
			   seasonIn, weekIn );

			var ds = CacheCommand( keyValue, commandStr, "sched", "ResultFor" );
			return ds;
		}

		public DataSet GetSeason( string seasonIn, string startWeek, string endWeek )
		{
			var keyValue = string.Format( "{0}:{1}:{2}:{3}", "GetSeason-DataSet",
										  seasonIn, startWeek, endWeek );
			string commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK>='{1}' and WEEK<='{2}'",
			   seasonIn, startWeek, endWeek );

			var ds = CacheCommand( keyValue, commandStr, "sched", "GetSeason" );
			return ds;
		}

		public DataSet GetSeason( string seasonIn )
		{
			var keyValue = string.Format( "{0}:{1}", "GetSeason2-DataSet",
										  seasonIn );
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' order by GAMEDATE, GAMEHOUR", seasonIn );

			var ds = CacheCommand( keyValue, commandStr, "sched", "GetSeason2" );

			return ds;
		}

		public DataTable GetSeasonDt( string seasonIn )
		{
			var ds = GetSeason( seasonIn );
			return ds.Tables[ "SCHED" ];
		}

		public DataSet GetAllGames()
		{
			const string commandStr = "SELECT * FROM SCHED";

			var ds = GetNflDataSet( "sched", commandStr );
			return ds;
		}

		public DataSet GetAllGames( int season )
		{
			var keyValue = string.Format( "{0}:{1}", "GetAllGames-DataSet",
										  season );
			var commandStr = string.Format( "SELECT * FROM SCHED WHERE SEASON = '{0}'", season );

			var ds = CacheCommand( keyValue, commandStr, "sched", "GetAllGames" );
			return ds;
		}

		public DataSet GetAllGames( string teamCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}')", teamCode );

			var ds = GetNflDataSet( "sched", commandStr );
			return ds;
		}

		public DataSet GetAllGames( string teamCode, string season )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') and SEASON='{1}' order by GAMEDATE",
			   teamCode, season );

			var ds = GetNflDataSet( "sched", commandStr );
			return ds;
		}

		public DataSet GetAllRegularSeasonGames( string teamCode, string season )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') and SEASON='{1}' and WEEK < '18' order by GAMEDATE",
			   teamCode, season );

			var ds = GetNflDataSet( "sched", commandStr );
			return ds;
		}

		public DataTable GetAllGamesDt( string teamCode )
		{
			DataSet ds = GetAllGames( teamCode );
			return ds.Tables[ "SCHED" ];
		}

		public DataTable GetAllGames( string teamCode, DateTime since )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') ORDER BY GAMEDATE", teamCode );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			foreach ( var dr in dt.Rows.Cast<DataRow>()
			   .Where( dr => DateTime.Parse( dr[ "GameDate" ].ToString() ) < since ) )
				dr.Delete(); //  game is too old
			return dt;
		}

		public DataRow GetGameByCode( string season, string week, string gameCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}'",
			   season, week, gameCode );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			return dt.Rows.Count > 0 ? dt.Rows[ 0 ] : null;
		}

		public DataRow GetGame( string season, string week, string teamCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1}' and (HOMETEAM='{2}' or AWAYTEAM='{2}')",
			   season, week, teamCode );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			return dt.Rows.Count > 0 ? dt.Rows[ 0 ] : null;
		}

		public string GetWeekFor( DateTime when )
		{
			var commandStr = string.Format( "SELECT * FROM SCHED where GAMEDATE = {{{0:MM/dd/yyyy}}}", when );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			return dt.Rows.Count > 0 ? dt.Rows[ 0 ][ "WEEK" ].ToString() : "0";
		}

		public DataRow GetWeekRecord( DateTime when )
		{
			var commandStr = string.Format( "SELECT * FROM SCHED where GAMEDATE = {{{0:MM/dd/yyyy}}}", when );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			if ( dt.Rows.Count > 0 )
				return dt.Rows[ 0 ];
			else
				return null;
		}

		public DataRow GetNflWeekFor( DateTime when )
		{
			var commandStr = string.Format( "SELECT * FROM SCHED where GAMEDATE = {{{0:MM/dd/yyyy}}}", when );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			return dt.Rows.Count > 0 ? dt.Rows[ 0 ] : null;
		}

		public string GetGameCode( string season, string week, string teamCode )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1}' and (HOMETEAM='{2}' or AWAYTEAM='{2}')",
			   season, week, teamCode );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var dr = dt.Rows[ 0 ];
			return dr[ "GAMENO" ].ToString();
		}

		public DataRow GetGame( string season, string week, string teamCode, string gameCode )
		{
			string commandStr = string.Format(
			   "SELECT * FROM SCHED where SEASON='{0}' and WEEK='{1}' and GAMENO='{2}' ",
			   season, week, gameCode );

			var ds = GetNflDataSet( "sched", commandStr );
			DataTable dt = ds.Tables[ "SCHED" ];
			DataRow dr = dt.Rows[ 0 ];
			return dr;
		}

		public DataRow GetGameAfter( string teamCode, DateTime when )
		{
			var season = "";
			var week = "";
			var gameNo = "";
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}') or (AWAYTEAM='{0}') order by GAMEDATE", teamCode );
			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			foreach ( DataRow dr in dt.Rows )
			{
				if ( DateTime.Parse( dr[ "GameDate" ].ToString() ) <= when ) continue;
				season = dr[ "SEASON" ].ToString();
				week = dr[ "WEEK" ].ToString();
				gameNo = dr[ "GAMENO" ].ToString();
				break;
			}
			DataRow row = null;
			if ( gameNo.Length > 0 )
				row = GetGame( season, week, teamCode, gameNo );
			return row;
		}

		public DataRow GetGamePriorTo( string teamCode, DateTime when )
		{
			var season = "";
			var week = "";
			var gameNo = "";
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}') or (AWAYTEAM='{0}') order by GAMEDATE", teamCode );
			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			foreach ( var dr in dt.Rows.Cast<DataRow>().TakeWhile( dr => DateTime.Parse( dr[ "GameDate" ].ToString() ) < when ) )
			{
				season = dr[ "SEASON" ].ToString();
				week = dr[ "WEEK" ].ToString();
				gameNo = dr[ "GAMENO" ].ToString();
			}
			DataRow row = null;
			if ( gameNo.Length > 0 )
				row = GetGame( season, week, teamCode, gameNo );
			return row;
		}

		public DataSet GetGamesBetween( string teamCodeOne, string teamCodeTwo, DateTime since )
		{
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' and AWAYTEAM='{1}') or (HOMETEAM='{1}' and AWAYTEAM='{0}')",
			   teamCodeOne, teamCodeTwo );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var aaWeek = new TimeSpan( 7, 0, 0, 0 );

			foreach ( DataRow dr in dt.Rows )
			{
				if ( DateTime.Parse( dr[ "GameDate" ].ToString() ) < since )
					dr.Delete(); //  game is too old
				else
				{
					if ( DateTime.Parse( dr[ "GameDate" ].ToString() ) > ( DateTime.Now - aaWeek ) )
						dr.Delete(); // game is a future game
				}
			}
			return ds;
		}

		public DataSet GetLastGame( string teamCode1, string teamCode2 )
		{
			//  Delete all but the last game
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' and AWAYTEAM='{1}') or (HOMETEAM='{1}' and AWAYTEAM='{0}') and (HOMESCORE>0 and AWAYSCORE>0) order by GAMEDATE",
			   teamCode1, teamCode2 );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var firstRec = true;
			foreach ( DataRow dr in dt.Rows )
			{
				if ( firstRec )
					firstRec = false;
				else
					dr.Delete();
			}
			return ds;
		}

		public DataSet GetLastGames( string teamCode, int nGames, int nSeason )
		{
			//  Delete all but the last game
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') and SEASON > '{1}' order by GAMEDATE DESCENDING",
			   teamCode, nSeason - 2 );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var nGameCount = 0;
			foreach ( DataRow dr in dt.Rows )
			{
				if ( GamePlayed( dr ) )
				{
					if ( nGameCount < nGames )
						nGameCount++;
					else
						dr.Delete();
				}
				else
					dr.Delete();
			}
			return ds;
		}

		public DataSet GetLastGames( string teamCode, int nGames, DateTime theDate )
		{
			//  Delete all but the last game
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') and GAMEDATE < {{{1:MM/dd/yyyy}}} order by GAMEDATE DESCENDING",
			   teamCode, theDate );

			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var nGameCount = 0;
			foreach ( DataRow dr in dt.Rows )
			{
				if ( GamePlayed( dr ) )
				{
					if ( nGameCount < nGames )
						nGameCount++;
					else
						dr.Delete();
				}
				else
					dr.Delete();
			}
			return ds;
		}

		public DataSet GetLastRegularSeasonGames( string teamCode, int nGames, DateTime theDate )
		{
			//  Delete all but the last game
			var commandStr = string.Format(
			   "SELECT * FROM SCHED where (HOMETEAM='{0}' or AWAYTEAM='{0}') and GAMEDATE < {{{1:MM/dd/yyyy}}} order by GAMEDATE DESCENDING",
			   teamCode, theDate );

#if DEBUG
         Console.WriteLine("Comand={0}", commandStr);
#endif
			var ds = GetNflDataSet( "sched", commandStr );
			var dt = ds.Tables[ "SCHED" ];
			var nGameCount = 0;
			foreach ( DataRow dr in dt.Rows )
			{
				if ( GamePlayed( dr ) )
				{
					if ( Int32.Parse( dr[ "WEEK" ].ToString() ) < 18 )
					{
						if ( nGameCount < nGames )
							nGameCount++;
						else
							dr.Delete();
					}
					else
						dr.Delete();
				}
				else
					dr.Delete();
			}
			return ds;
		}

		private static bool GamePlayed( DataRow dr )
		{
			var nHomeScore = Int32.Parse( dr[ "HOMESCORE" ].ToString() );
			var nAwayScore = Int32.Parse( dr[ "AWAYSCORE" ].ToString() );
			var gameDate = DateTime.Parse( dr[ "GAMEDATE" ].ToString() );
			return ( !nHomeScore.Equals( 0 ) || !nAwayScore.Equals( 0 ) ) && ( gameDate <= DateTime.Now );
		}

		#endregion SCHED

		#region PREDICTION

		public DataSet GetAllPredictions( string season, string method )
		{
			var commandStr =
			   string.Format( "select * from PREDICTION where SEASON='{0}' and METHOD='{1}'",
				  season, method );

			return GetNflDataSet( "prediction", commandStr );
		}

		public DataSet GetPrediction( string method, string season, string week, string gameCode )
		{
			var commandStr =
			   string.Format(
				  "select * from PREDICTION where SEASON='{0}'"
				  + " and " + "WEEK='{1}' and GAMECODE = '{2}' and METHOD='{3}'",
				  season, week, gameCode, method );

			return GetNflDataSet( "prediction", commandStr );
		}

		public DataSet GetPrediction( string method, string season, string week )
		{
			var commandStr =
			   string.Format(
				  "select * from PREDICTION where SEASON='{0}'"
				  + " and " + "WEEK='{1}' and METHOD='{2}'",
				  season, week, method );

			return GetNflDataSet( "prediction", commandStr );
		}

		public void InsertPrediction( string method, string season, string week, string gameCode,
		   int homeScore, int awayScore, int htdp, int htdr, int htdd, int htds, int hfg,
		   int atdp, int atdr, int atdd, int atds, int afg,
		   int hydp, int hydr, int aydp, int aydr
		   )
		{
			var formatStr =
			   "INSERT INTO PREDICTION (METHOD, SEASON, WEEK, GAMECODE, HOMESCORE, AWAYSCORE, HTDP, HTDR, HTDD, HTDS, HFG, ATDP, ATDR, ATDD, ATDS, AFG, HYDP, HYDR, AYDP, AYDR)";
			formatStr += "VALUES( '{0}','{1}','{2}','{3}',{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19} )";
			var commandStr = string.Format( formatStr, method, season, week, gameCode, homeScore, awayScore,
													   htdp, htdr, htdd, htds, hfg, atdp, atdr, atdd, atds, afg,
													   hydp, hydr, aydp, aydr
													   );
			ExecuteNflCommand( commandStr );
		}

		public void UpdatePrediction( string method, string season, string week, string gameCode, int homeScore, int awayScore,
		   int htdp, int htdr, int htdd, int htds, int hfg,
		   int atdp, int atdr, int atdd, int atds, int afg,
		   int hydp, int hydr, int aydp, int aydr
		   )
		{
			var formatStr = "UPDATE PREDICTION SET AWAYSCORE = {0}, HOMESCORE={1}, HTDP={6}, HTDR={7}, HTDD={8}, HTDS={9}, HFG={10}, ATDP={11}, ATDR={12}, ATDD={13}, ATDS={14}, AFG={15}, HYDP={16}, HYDR={17}, AYDP={18}, AYDR={19}";
			formatStr += " WHERE SEASON='{2}' AND WEEK='{3}' AND GAMECODE='{4}' and METHOD='{5}'";
			var commandStr = string.Format( formatStr, awayScore, homeScore, season, week, gameCode, method,
													   htdp, htdr, htdd, htds, hfg, atdp, atdr, atdd, atds, afg,
													   hydp, hydr, aydp, aydr
													   );
			ExecuteNflCommand( commandStr );
		}

		#endregion PREDICTION

		#region ACE

		public DataSet GetAce( string season, string week, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			var commandStr =
			   string.Format(
				  "select * from ACE where SEASON='{0}'"
				  + " and " + "WEEK='{1}' and PLAYERID = '{2}'",
				  season, week, playerId );

			return GetNflDataSet( "ace", commandStr );
		}

		public void DeleteTeamAces( string teamCode, string season, int week )
		{
			var formatStr =
			   "DELETE FROM ACE WHERE TEAMCODE = '{0}' AND SEASON = '{1}' AND WEEK = '{2}'";
			var commandStr = string.Format( formatStr,
				  teamCode, season, string.Format( "{0:00}", week )
			   );
			ExecuteNflCommand( commandStr );
		}

		public void UpdateAce( string season, string week, string playerId, string playerCat, string teamCode,
		   decimal load, int touches
		   )
		{
			var formatStr = "UPDATE ACE SET LOAD = {0}, TOUCHES={1}";
			formatStr += " WHERE SEASON='{2}' AND WEEK='{3}' AND PLAYERID ='{4}'";
			var commandStr = string.Format( formatStr, load, touches, season, week, playerId );
			ExecuteNflCommand( commandStr );
		}

		public void InsertAce( string season, string week, string teamCode, string playerId, string playerCat,
		   decimal load, int touches
		   )
		{
			var formatStr =
			   "INSERT INTO ACE (SEASON, WEEK, TEAMCODE, PLAYERID, PLAYERCAT, LOAD, TOUCHES)";
			formatStr += "VALUES( '{0}','{1}','{2}','{3}',{4},{5},{6}  )";
			var commandStr = string.Format( formatStr, season, week, teamCode,
													   playerId, playerCat, load, touches
													   );
			ExecuteNflCommand( commandStr );
		}

		#endregion ACE

		#region URATINGS

		//  Unit ratings table has all the ratings that have been generated (assumes they are correct)
		//  The ratings values are those going into the contest identified by the NFL sunday of the week

		public DataSet GetUnitRatings( DateTime when )
		{
			var keyValue = string.Format( "{0}:{1}", "GetUnitRatings-DataSet",
				when.Date.ToLongDateString() );

			var commandStr =
			   string.Format( "select * from URATINGS where SUNDAY={{{0:MM/dd/yyyy}}}",
			   when );

			var ds = CacheCommand( keyValue, commandStr, "uratings", "GetUnitRatings" );
			return ds;
		}

		public string GetUnitRatings( DateTime when, string teamCode )
		{
			var keyValue = $"{"GetUnitRatings2-DataSet"}:{when.Date.ToLongDateString()}:{teamCode}";

			var commandStr =
			   string.Format( "select * from URATINGS where TEAMCODE = '{1}' AND SUNDAY={{{0:MM/dd/yyyy}}}", when, teamCode );

			var ds = CacheCommand( keyValue, commandStr, "uratings", "GetUnitRatings2" );

			var ratings = "??????";
			if ( ds.Tables[ 0 ].Rows.Count > 0 )
				ratings = ds.Tables[ 0 ].Rows[ 0 ][ "ratings" ].ToString();
			return ratings;
		}

		public void SaveUnitRatings( string ratings, DateTime when, string teamCode )
		{
			if ( when != new DateTime( 1, 1, 1 ) )
			{
				var oldRatings = GetUnitRatings( when, teamCode );
				string commandStr = string.Empty;
				if ( oldRatings == "??????" )
				{
					var formatStr = "INSERT INTO URATINGS (SUNDAY, TEAMCODE, RATINGS)";
					formatStr += "VALUES( {{{0:MM/dd/yyyy}}},'{1}','{2}' )";
					commandStr = string.Format( formatStr, when, teamCode, ratings );
				}
				else
				{
					var formatStr = "UPDATE URATINGS SET RATINGS = '{0}' ";
					formatStr += "WHERE TEAMCODE = '{1}' AND SUNDAY = {{{2:MM/dd/yyyy}}}";
					commandStr = string.Format( formatStr, ratings, teamCode, when );
				}
				//IoInfo( commandStr );
				ExecuteNflCommand( commandStr );
			}
		}

		#endregion URATINGS

		#region RUNS (TYCOON DB)

		public void InsertRun( string stepName, TimeSpan ts, string category )
		{
			var formatStr = "INSERT INTO RUNS (STEP, FINISHED, HRS, MINS, SECS, MACHINE, CATEGORY, FINISHAT)";
			formatStr += "VALUES( '{0}',{{{1:MM/dd/yyyy}}}, {2}, {3},{4},'{5}', '{6}', '{7}' )";
			var machineName = Environment.MachineName;
			var commandStr = string.Format( formatStr,
			   stepName, DateTime.Now, ts.Hours, ts.Minutes, ts.Seconds, machineName, category,
			   DateTime.Now.ToShortTimeString() );
			OleDbConnTycoon.Close();
			OleDbConnTycoon.Open();
			var cmd = new OleDbCommand( commandStr, OleDbConnTycoon );
			cmd.ExecuteNonQuery();
			OleDbConnTycoon.Close();
		}

		public void InsertRun( string stepName, TimeSpan ts )
		{
			InsertRun( stepName, ts, "Unspecified" );
		}

		private static string Dtos( DateTime theDate )
		{
			return string.Format( "{0}{1:00}{2:00}", theDate.Year, theDate.Month, theDate.Day );
		}

		public DateTime GetLastRun( string reportName )
		{
			const string formatStr = "SELECT * FROM RUNS WHERE STEP = '{0}' order by FINISHED DESC";
			var commandStr = string.Format( formatStr, reportName );
			OleDbConnTycoon.Close();
			var da = new OleDbDataAdapter( commandStr, OleDbConnTycoon );
			var ds = new DataSet();
			da.Fill( ds, "runs" );
			var dt = ds.Tables[ "RUNS" ];
			return dt.Rows.Count > 0 ?
			   DateTime.Parse( dt.Rows[ 0 ][ "FINISHED" ].ToString() ) : new DateTime( 1, 1, 1 );
		}

		public DataTable GetRuns( DateTime sinceDate )
		{
			const string formatStr =
			   "SELECT * FROM RUNS WHERE FINISHED > {{{0:MM/dd/yyyy}}} order by FINISHED DESC";
			var commandStr = string.Format( formatStr, sinceDate.Date );
			OleDbConnTycoon.Close();
			var da = new OleDbDataAdapter( commandStr, OleDbConnTycoon );
			var ds = new DataSet();
			da.Fill( ds, "runs" );
			return ds.Tables[ "RUNS" ];
		}

		#endregion RUNS (TYCOON DB)

		#region UNITPERF

		public void InsertUnitPerformance( string teamCode, string unitCode, string season, int week,
		   string opponent, string leader, string oppLeader, string unitRate, string oppRate,
		   int yds, int tds, int ints, decimal sacks )
		{
			var formatStr =
			   "INSERT INTO UNITPERF (TEAMCODE, UNIT, SEASON, WEEK, OPP, LDR, OPPLDR, UNITRATE, OPPRATE, YDS, TDS, INTS, SAKS )";
			formatStr += "VALUES( '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}',{9},{10},{11},{12} )";
			var commandStr = string.Format( formatStr,
				  teamCode, unitCode, season, string.Format( "{0:00}", week ), opponent, leader, oppLeader, unitRate, oppRate,
				  yds, tds, ints, sacks
			   );
			ExecuteNflCommand( commandStr );
		}

		public DataRow GetUnitPerformance( string teamCode, string season, int week, string unitCode )
		{
			var formatStr =
			   "SELECT * FROM UNITPERF WHERE TEAMCODE = '{0}' AND SEASON = '{1}' AND WEEK = '{2}' AND UNIT ='{3}'";
			var commandStr = string.Format( formatStr,
				  teamCode, season, string.Format( "{0:00}", week ), unitCode
			   );
			return GetNflDataRow( "unitperf", commandStr );
		}

		public void DeleteUnitPerformance( string teamCode, string season, int week, string unitCode )
		{
			var formatStr =
			   "DELETE FROM UNITPERF WHERE TEAMCODE = '{0}' AND SEASON = '{1}' AND WEEK = '{2}' AND UNIT ='{3}'";
			var commandStr = string.Format( formatStr,
				  teamCode, season, string.Format( "{0:00}", week ), unitCode
			   );
			ExecuteNflCommand( commandStr );
		}

		#endregion UNITPERF

		#region Other Storing

		public void StorePlayerRoleAndPos( string role, string pos, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			var formatStr = "UPDATE PLAYER SET ROLE = '{0}', POSDESC='{1}'";
			formatStr += " WHERE PLAYERID='{2}' ";
			var commandStr = string.Format( formatStr, role, pos, playerId );

			OleDbConn.Close();
			OleDbConn.Open();
			var cmd = new OleDbCommand( commandStr, OleDbConn );
			cmd.ExecuteNonQuery();
			OleDbConn.Close();
		}

        public void StoreJersey( string jerseyNo, string playerId )
        {
            playerId = FixSingleQuotes( playerId );

            string commandStr = $@"UPDATE PLAYER SET JERSEY = {
                jerseyNo
                } WHERE PLAYERID='{playerId}'";

            ExecuteNflCommand( commandStr );
        }

        public void StoreProjection( int nProjected, string playerId )
		{
			playerId = FixSingleQuotes( playerId );

			string commandStr = string.Format(
			   "UPDATE PLAYER SET PROJECTED = '{0}' WHERE PLAYERID='{1}'",
			   nProjected, playerId );

			ExecuteNflCommand( commandStr );
		}

		public void StoreResult( string season, string week, string gameNo, int awayScore, int homeScore,
								int homeTdp, int awayTdp, int homeTdr, int awayTdr, int homeFg, int awayFg,
								int awayTdd, int homeTdd, int awayTds, int homeTds )
		{
			var formatStr = "UPDATE SCHED SET AWAYSCORE = {0}, HOMESCORE={1}";
			formatStr += ", HOME_TDP = {5}, AWAY_TDP = {6}";
			formatStr += ", HOME_TDR = {7}, AWAY_TDR = {8}";
			formatStr += ", HOME_FG = {9}, AWAY_FG = {10}";
			formatStr += ", HOME_TDD= {11}, AWAY_TDD = {12}";
			formatStr += ", HOME_TDS = {13}, AWAY_TDS = {14}";
			formatStr += " WHERE SEASON='{2}' AND WEEK='{3}' AND GAMENO='{4}'";
			var commandStr = string.Format( formatStr, awayScore, homeScore, season, week, gameNo,
										   homeTdp, awayTdp, homeTdr, awayTdr, homeFg, awayFg, homeTdd, awayTdd, homeTds, awayTds );
			ExecuteNflCommand( commandStr );
		}

		public void StorePlayer( string playerId, string firstname, string surname, string teamCode,
								string role, int heightFeet, int heightInches, int weight, string college, string rookieYr,
								string posdesc, string category, string dob )
		{
			playerId = FixSingleQuotes( playerId );
			var formatStr =
			   "INSERT INTO PLAYER (PLAYERID, SURNAME, FIRSTNAME, CURRTEAM, ROLE, HEIGHT_FT, HEIGHT_IN, WEIGHT, COLLEGE, ROOKIEYR, POSDESC, CATEGORY, DOB)";
			formatStr += "VALUES( {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12} )";

			var commandStr = string.Format( formatStr, playerId, firstname, surname, teamCode,
				   role, heightFeet, heightInches, weight, college, rookieYr, posdesc, category, dob );

			ExecuteNflCommand( commandStr );
		}

		public void CloseServitude( DateTime closeDate, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			//UPDATE SERVE SET SERVE.TO = CTOD("22/04/2015") WHERE PLAYERID='PETEAD02' and SERVE.TO =CTOD('  /  /  ')
			var formatStr = "UPDATE SERVE SET SERVE.TO = CTOD('{0:MM/dd/yyyy}')";
			formatStr += " WHERE PLAYERID='{1}' and SERVE.TO =CTOD('  /  /  ')";
			var commandStr = string.Format( formatStr, closeDate, playerId );

			ExecuteNflCommand( commandStr );
		}

		public void RetirePlayer( DateTime closeDate, string playerId )
		{
			playerId = FixSingleQuotes( playerId );
			//UPDATE SERVE SET SERVE.HOW = SERVE.HOW + ".RET"  WHERE PLAYERID='PETEAD02' and SERVE.TO = ctod('22/04/2015')
			var formatStr = "UPDATE SERVE SET SERVE.TO = CTOD('{0:MM/dd/yyyy}'), SERVE.HOW = '.RET' ";
			formatStr += " WHERE PLAYERID='{1}' and SERVE.TO =CTOD('  /  /  ')";
			var commandStr = string.Format( formatStr, closeDate, playerId );

			ExecuteNflCommand( commandStr );
		}

		public void SetCurrentTeam( string playerId, string teamCode )
		{
			playerId = FixSingleQuotes( playerId );
			var commandStr =
			   string.Format( "UPDATE PLAYER SET CURRTEAM = '{1}' WHERE PLAYERID='{0}'", playerId, teamCode );

			ExecuteNflCommand( commandStr );
		}

		public void SetRole( string playerId, string roleCode )
		{
			playerId = FixSingleQuotes( playerId );
			var commandStr =
			   $"UPDATE PLAYER SET ROLE = '{roleCode}' WHERE PLAYERID='{playerId}'";

			ExecuteNflCommand( commandStr );
		}

        public void ClearDefensiveRoles( string teamCode )
        {
            var commandStr =
               $@"UPDATE PLAYER SET ROLE = ' ' WHERE CURRTEAM='{
                   teamCode
                   }' and (CATEGORY = '5' or CATEGORY = '6')";

            ExecuteNflCommand( commandStr );
        }

        public void Sign( string playerId, string teamCode, DateTime when, string how )
		{
			playerId = FixSingleQuotes( playerId );
			var formatStr =
			   "INSERT INTO SERVE (PLAYERID, TEAMID, FROM, TO, HOW)";
			formatStr += " VALUES( '{0}','{1}',ctod('{2:MM/dd/yyyy}'),ctod('  /  /  '),'{3}' )";
			var commandStr =
			   string.Format( formatStr, playerId, teamCode, when, how );

			ExecuteNflCommand( commandStr );
		}

		#endregion Other Storing

		#region PGMETRIC

		public DataSet GetPlayerGameMetrics( string playerCode, string gameCode )
		{
			playerCode = FixSingleQuotes( playerCode );
			var keyValue = string.Format( "{0}:{1}:{2}", "GetPlayerGameMetrics-DataSet",
						   playerCode, gameCode );
			var commandStr =
			   string.Format(
				  "select * from PGMETRIC where PLAYERID='{0}' and GAMECODE = '{1}'",
				  playerCode, gameCode );

			return NoCacheCommand( keyValue, commandStr, "pgmetrics", "GetPlayerGameMetrics" );
		}

		public void InsertPlayerGameMetric( string playerId, string gameCode,
		   int projYDp, int YDp, int projYDr, int ydr,
		   int projTDp, int TDp, decimal projTDr, int tdr,
		   int projTDc, int TDc, int projYDc, int YDc,
		   int projFG, int fg, int projPat, int pat, decimal fpts
		   )
		{
			var plyrId = playerId.Replace( "'", "''" );
			var formatStr =
			   "INSERT INTO PGMETRIC (PLAYERID, GAMECODE, projYDp, YDp, projYDr, ydr, projTDp, TDp, projTDr, tdr, projTDc, TDc, projYDc, YDc, projFG, FG, ProjPat, PAT, YahooPts)";
			formatStr += "VALUES( '{0}','{1}',{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18} )";
			var commandStr = string.Format( formatStr, plyrId, gameCode,
				 projYDp, YDp, projYDr, ydr, projTDp, TDp, projTDr, tdr, projTDc, TDc, projYDc, YDc, projFG, fg, projPat, pat, fpts );
			ExecuteNflCommand( commandStr );
		}

		public string UpdatePlayerGameMetric( string playerId, string gameCode,
		   int projYDp, int YDp, int projYDr, int ydr,
		   int projTDp, int TDp, decimal projTDr, int tdr,
		   int projTDc, int TDc, int projYDc, int YDc,
		   int projFG, int fg, int projPat, int pat
		   )
		{
			playerId = playerId.Replace( "'", "''" );
			var formatStr = "UPDATE PGMETRIC SET projYDp={2}, YDp={3}, projYDr={4}, YDR={5}, projTDP={6}, tdp={7}, projTDr={8}, TDr={9}, projTDc={10}, TDc={11}, projYDc={12}, YDc={13}, FG={14}, PAT={15},projFG={16}, projPAT={17}";
			formatStr += " WHERE PLAYERID='{0}' AND GAMECODE='{1}'";
			var commandStr = string.Format( formatStr, playerId, gameCode,
			   projYDp, YDp, projYDr, ydr, projTDp, TDp, projTDr, tdr, projTDc, TDc, projYDc, YDc, fg, pat, projFG, projPat );
			ExecuteNflCommand( commandStr );
			return commandStr;
		}

		public string UpdatePlayerGameMetricWithActuals( string playerId, string gameCode,
			int YDp, int ydr,
			int TDp, int tdr,
			int TDc, int YDc,
			int fg, int pat,
			   decimal fp
		   )
		{
			playerId = playerId.Replace( "'", "''" );
			var formatStr = "UPDATE PGMETRIC SET YDp={2}, YDR={3}, tdp={4}, TDr={5}, TDc={6}, YDc={7}, FG={8}, PAT={9}, YAHOOPTS={10}";
			formatStr += " WHERE PLAYERID='{0}' AND GAMECODE='{1}'";
			var commandStr = string.Format( formatStr, playerId, gameCode, YDp, ydr, TDp, tdr, TDc, YDc, fg, pat, fp );
			ExecuteNflCommand( commandStr );
			return commandStr;
		}

		public DataSet GetPlayerGameMetrics( string gameCode )
		{
			var keyValue = string.Format( "{0}:{1}", "GetPlayerGameMetrics-DataSet",
							  gameCode );

			var commandStr =
			   $"select * from PGMETRIC where GAMECODE = '{gameCode}'";

			return CacheCommand( keyValue, commandStr, "pgmetrics", "GetPlayerGameMetrics" );
		}

		public DataSet GetAllPlayerGameMetrics( string season, string week )
		{
			var keyValue = string.Format( "{0}:{1}:{2}", "GetAllPlayerGameMetrics-DataSet",
							  season, week );
			var gameCode = string.Format( "{0}:{1}-", season, week );
			var commandStr =
			   string.Format(
				  "select * from PGMETRIC where GAMECODE like'%{0}%'", gameCode );

			return CacheCommand( keyValue, commandStr, "pgmetrics", "GetAllPlayerGameMetrics" );
		}

		public DataSet GetAllPlayerGameMetrics( string season )
		{
			var keyValue = string.Format( "{0}:{1}", "GetAllPlayerGameMetrics2-DataSet",
					 season );
			var gameCode = string.Format( "{0}:", season );
			var commandStr =
			   string.Format(
				  "select * from PGMETRIC where GAMECODE like'%{0}%'", gameCode );
			return CacheCommand( keyValue, commandStr, "pgmetrics", "GetAllPlayerGameMetrics2" );
		}

		public DataSet GetAllPlayerGameMetricsForPlayer( string season, string playerCode )
		{
			playerCode = playerCode.Replace( "'", "''" );
			var keyValue = string.Format( "{0}:{1}:{2}", "GetAllPlayerGameMetricsForPlayer-DataSet",
							  season, playerCode );
			var gameCode = string.Format( "{0}:", season );
			var commandStr =
			   string.Format(
				  "select * from PGMETRIC where GAMECODE like'%{0}%' and PLAYERID = '{1}'",
				  gameCode, playerCode );

			return CacheCommand( keyValue, commandStr, "pgmetrics", "GetAllPlayerGameMetricsForPlayer" );
		}

		public void ClearPlayerGameMetrics( string gameKey )
		{
			var commandStr = string.Format( "DELETE FROM PGMETRIC where GAMECODE = '{0}'", gameKey );
			ExecuteNflCommand( commandStr );
		}

		#endregion PGMETRIC

		#region Utility

		private static string FixSingleQuotes( string playerId )
		{
			if ( playerId.Contains( '\'' ) )
				playerId = playerId.Replace( "'", "''" );
			return playerId;
		}

		private void ExecuteNflCommand( string commandStr )
		{
			try
			{
				OleDbConn.Close();
				OleDbConn.Open();
				var cmd = new OleDbCommand( commandStr, OleDbConn );
				cmd.ExecuteNonQuery();
				OleDbConn.Close();
				IoTrace( commandStr );
			}
			catch ( Exception ex )
			{
				WriteToLog( $"Failure to execute Command: {commandStr} >> {ex.Message} " );
				throw;
			}
		}

		private DataRow GetNflDataRow( string tableName, string commandStr )
		{
			IoTrace( commandStr );
			OleDbConn.Close();
			var da = new OleDbDataAdapter( commandStr, OleDbConn );
			var ds = new DataSet();
			da.Fill( ds, tableName );
			DataTable dt = ds.Tables[ tableName ];
			if ( dt.Rows.Count > 0 )
				return dt.Rows[ 0 ];
			return null;
		}

		private DataSet GetNflDataSet( 
			string tableName, 
			string commandStr, 
			string caller = "", 
			bool logit = false )
		{
			if ( logit )
			{
				IoInfo( commandStr, caller );
			}
			var da = new OleDbDataAdapter( commandStr, OleDbConn );
			var ds = new DataSet();
			da.Fill( ds, tableName );
			return ds;
		}

		public void WriteToLog( string msg )
		{
			if ( Logger == null ) Logger = LogManager.GetCurrentClassLogger();
			Logger.Info( msg );
		}

		private static int ioCount = 0;

		public void IoTrace( string ioCommand, string caller = "" )
		{
			if ( Logger == null ) Logger = LogManager.GetCurrentClassLogger();
			ioCount++;
			Logger.Trace( string.Format( "   ({0}) >>>{1}  {2}",
			   ioCount, ioCommand, caller ) );
		}

		public void IoInfo( string ioCommand, string caller = "" )
		{
			if ( Logger == null ) Logger = LogManager.GetCurrentClassLogger();
			Logger.Info( $"   ({ioCommand}) >>>{caller}" );
		}

		private static int hitCount = 0;

		public void LogCacheHit( string keyValue, string caller = "" )
		{
			if ( Logger == null ) Logger = LogManager.GetCurrentClassLogger();
			hitCount++;
			Logger.Trace( $"   cache ({hitCount}) hit ({keyValue}) {caller}" );
		}


		private DataSet CacheCommand( string keyValue,
			  string commandStr, string dsName, string caller = "", TimeSpan? timeSpan = null )
		{
			//UseCache = true;  //Best way to control this atm is just not to run the redis server
			if ( !UseCache )
				return NoCacheCommand(keyValue, commandStr, dsName, caller);

			if ( timeSpan == null )	timeSpan = TimeSpan.FromHours( 4 );

			if ( !cache.TryGet( keyValue, out DataSet ds ) )
			{
				ds = GetNflDataSet( dsName, commandStr, caller );
				cache.Set( keyValue, ds, timeSpan );
			}
			else
				LogCacheHit( keyValue, caller );
			return ds;
		}

		private DataSet NoCacheCommand(
		   string keyValue,
		   string commandStr, string dsName, string caller = "" )
		{
			var ds = GetNflDataSet(
                tableName: dsName,
                commandStr: commandStr,
                caller: caller,
                logit: true);
			return ds;
		}

		#endregion Utility
	}
}