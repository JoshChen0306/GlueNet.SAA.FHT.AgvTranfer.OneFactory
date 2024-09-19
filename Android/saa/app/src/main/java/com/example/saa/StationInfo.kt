package com.example.saa

data class StationInfo(
    val stationNo: String,
    val haveFlag: String?,
    val bgnToEnd: String?,
    val useFlag:String?,
    val rackID:String?
){
    override fun toString(): String {
        return "Station:" + stationNo
    }
}
