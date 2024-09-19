package com.example.saa

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.sql.Connection
import java.sql.DriverManager
import java.sql.PreparedStatement
import java.sql.ResultSet
import java.sql.SQLException
import java.text.SimpleDateFormat
import java.util.Date

class DatabaseHelper {
    companion object {
        private const val JDBC_DRIVER = "net.sourceforge.jtds.jdbc.Driver"
        private const val DB_URL = "jdbc:jtds:sqlserver://192.168.4.10/agvDB_1400004"
        //private const val DB_URL = "jdbc:jtds:sqlserver://192.168.0.111/agvDB_1400004"
        private const val USER = "mcs"
        private const val PASS = "Zz123456"
    }

    init {
        // Register JDBC driver
        Class.forName(JDBC_DRIVER)
    }

    suspend fun checkDatabaseConnection(): Boolean {
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                true // Connection successful
            } catch (e: Exception) {
                e.printStackTrace()
                false // Connection failed
            } finally {
                connection?.close()
            }
        }
    }

    suspend fun checkLogin(username: String, password: String): Boolean {
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            var resultSet: ResultSet? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "SELECT * FROM pUser WHERE userId = ? AND password = ?"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, username)
                preparedStatement.setString(2, password)
                resultSet = preparedStatement.executeQuery()
                resultSet.next() // Returns true if there is a match
            } catch (e: Exception) {
                e.printStackTrace()
                false
            } finally {
                resultSet?.close()
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    suspend fun send_oNeed(start: String, end: String?, rackId: String?, workOrder:String?, assignFlag:String?): Boolean {
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "INSERT INTO oNeed (ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime, AssignFlag) VALUES (?, ?, ?, ?, ?, ?,?)"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, start)
                preparedStatement.setString(2, rackId)
                preparedStatement.setString(3, workOrder)
                preparedStatement.setString(4, end)
                preparedStatement.setString(5, "APP")
                preparedStatement.setString(6, getCurrentFormattedTime()+"000000")
                preparedStatement.setString(7, assignFlag)
                preparedStatement.executeUpdate() > 0 // Returns true if the insert was successful
            } catch (e: Exception) {
                e.printStackTrace()
                false
            } finally {
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    fun getCurrentFormattedTime(): String {
        val dateFormat = SimpleDateFormat("yyyyMMddHHmmss")
        val date = Date()
        return dateFormat.format(date)
    }

    suspend fun get_rackId(station: String):String?{
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            var resultSet: ResultSet? = null
            var rackId: String? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "SELECT RackId FROM oPort WHERE StationNo = ?"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, station)
                resultSet = preparedStatement.executeQuery()
                if (resultSet.next()) {
                    rackId = resultSet.getString("RackId")
                }
                rackId
            } catch (e: SQLException) {
                e.printStackTrace()
                null
            } finally {
                resultSet?.close()
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    suspend fun change_oPort(station:String,rackId:String,haveFlag:String,workOrder:String?): Boolean {
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "UPDATE oPort SET RackId = ?, WorkOrder = ?, HaveFlag = ? WHERE StationNo = ?"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, rackId)
                preparedStatement.setString(2, workOrder)
                preparedStatement.setString(3, haveFlag)
                preparedStatement.setString(4, station)
                preparedStatement.executeUpdate() > 0 // Returns true if the insert was successful
            } catch (e: Exception) {
                e.printStackTrace()
                false
            } finally {
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    suspend fun get_userGroup(username: String, password: String):String?{
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            var resultSet: ResultSet? = null
            var groupId: String? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "SELECT * FROM pUser WHERE UserId = ? AND Password = ?"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, username)
                preparedStatement.setString(2, password)
                resultSet = preparedStatement.executeQuery()
                if (resultSet.next()) {
                    groupId = resultSet.getString("groupId")
                }
                groupId
            } catch (e: SQLException) {
                e.printStackTrace()
                null
            } finally {
                resultSet?.close()
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    fun get_oUser(username: String, password: String): oUserModel {
        var connection: Connection? = null
        var preparedStatement: PreparedStatement? = null
        var resultSet: ResultSet? = null
        try {
            connection = DriverManager.getConnection(DB_URL, USER, PASS)
            val sql = "SELECT * FROM pUser WHERE UserId = ? AND Password = ?"
            preparedStatement = connection.prepareStatement(sql)
            preparedStatement.setString(1, username)
            preparedStatement.setString(2, password)
            resultSet = preparedStatement.executeQuery()
            while (resultSet.next()) {
                val UserId = resultSet.getString("UserId")
                val UserName = resultSet.getString("UserName")
                val Password = resultSet.getString("Password")
                val GroupId = resultSet.getString("GroupId")
                val Mail = resultSet.getString("Mail")
                val Tel = resultSet.getString("Tel")
                val ModifiedTime = resultSet.getString("ModifiedTime")
                return oUserModel(UserId, UserName, Password, GroupId, Mail, Tel, ModifiedTime)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        } finally {
            resultSet?.close()
            preparedStatement?.close()
            connection?.close()
        }

        return oUserModel("","","","","", "", "")
    }

    suspend fun get_oport(station: String): oPortModel {
        return withContext(Dispatchers.IO) {
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            var resultSet: ResultSet? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "SELECT * FROM oPort WHERE StationNo = ?"
                preparedStatement = connection.prepareStatement(sql)
                preparedStatement.setString(1, station)
                resultSet = preparedStatement.executeQuery()
                if (resultSet.next()) {
                    val Area = resultSet.getString("Area")
                    val Block = resultSet.getString("Block")
                    val Port = resultSet.getString("Port")
                    val StationNo = resultSet.getString("StationNo")
                    val InterfaceName = resultSet.getString("InterfaceName")
                    val Priority = resultSet.getString("Priority")
                    val UseFlag = resultSet.getString("UseFlag")
                    val RackId = resultSet.getString("RackId")
                    val WorkOrder = resultSet.getString("WorkOrder")
                    val HaveFlag = resultSet.getString("HaveFlag")
                    val BgnToEnd = resultSet.getString("BgnToEnd")
                    val MachineName = resultSet.getString("MachineName")
                    oPortModel(Area, Block, Port, StationNo, InterfaceName, Priority, UseFlag, RackId, WorkOrder, HaveFlag, BgnToEnd, MachineName)
                } else {
                    // Handle the case where no rows are returned, e.g., return a default model
                    oPortModel("", "", "", "", "", "", "", "", "", "", "", "")
                }
            } catch (e: Exception) {
                e.printStackTrace()
                oPortModel("", "", "", "", "", "", "", "", "", "", "", "")
            } finally {
                resultSet?.close()
                preparedStatement?.close()
                connection?.close()
            }
        }
    }

    fun getbyInterface(station: String?): oPortModel {
        var connection: Connection? = null
        var preparedStatement: PreparedStatement? = null
        var resultSet: ResultSet? = null
        try {
            connection = DriverManager.getConnection(DB_URL, USER, PASS)
            val sql = "SELECT * FROM oPort WHERE InterfaceName = ?"
            preparedStatement = connection.prepareStatement(sql)
            preparedStatement.setString(1, station)
            resultSet = preparedStatement.executeQuery()
            while (resultSet.next()) {
                val Area = resultSet.getString("Area") ?: ""
                val Block = resultSet.getString("Block") ?: ""
                val Port = resultSet.getString("Port") ?: ""
                val StationNo = resultSet.getString("StationNo") ?: ""
                val InterfaceName = resultSet.getString("InterfaceName") ?: ""
                val Priority = resultSet.getString("Priority") ?: ""
                val UseFlag = resultSet.getString("UseFlag") ?: ""
                val RackId = resultSet.getString("RackId") ?: ""
                val WorkOrder = resultSet.getString("WorkOrder") ?: ""
                val HaveFlag = resultSet.getString("HaveFlag") ?: ""
                val BgnToEnd = resultSet.getString("BgnToEnd") ?: ""
                val MachineName = resultSet.getString("MachineName") ?: ""
                return oPortModel(Area, Block, Port, StationNo, InterfaceName, Priority, UseFlag, RackId, WorkOrder, HaveFlag, BgnToEnd, MachineName)
            }
        } catch (e: Exception) {
            e.printStackTrace()
        } finally {
            resultSet?.close()
            preparedStatement?.close()
            connection?.close()
        }

        return oPortModel("", "", "", "", "", "", "", "", "", "", "", "")
    }

    suspend fun getAlloPort(): List<oPortModel> {
        return withContext(Dispatchers.IO) {
            val portList = mutableListOf<oPortModel>()
            var connection: Connection? = null
            var preparedStatement: PreparedStatement? = null
            var resultSet: ResultSet? = null
            try {
                connection = DriverManager.getConnection(DB_URL, USER, PASS)
                val sql = "SELECT * FROM oPort"
                preparedStatement = connection.prepareStatement(sql)
                resultSet = preparedStatement.executeQuery()
                while (resultSet.next()) {
                    val Area = resultSet.getString("Area")
                    val Block = resultSet.getString("Block")
                    val Port = resultSet.getString("Port")
                    val StationNo = resultSet.getString("StationNo")
                    val InterfaceName = resultSet.getString("InterfaceName")
                    val Priority = resultSet.getString("Priority")
                    val UseFlag = resultSet.getString("UseFlag")
                    val RackId = resultSet.getString("RackId")
                    val WorkOrder = resultSet.getString("WorkOrder")
                    val HaveFlag = resultSet.getString("HaveFlag")
                    val BgnToEnd = resultSet.getString("BgnToEnd")
                    val MachineName = resultSet.getString("MachineName")
                    portList.add(oPortModel(Area, Block, Port, StationNo, InterfaceName, Priority, UseFlag, RackId, WorkOrder, HaveFlag, BgnToEnd, MachineName))
                }
            } catch (e: Exception) {
                e.printStackTrace()
            } finally {
                resultSet?.close()
                preparedStatement?.close()
                connection?.close()
            }

            portList
        }
    }
}
