package com.example.saa

import android.os.Parcel
import android.os.Parcelable

data class oPortModel(
    var Area: String,
    var Block: String,
    var Port: String,
    var StationNo: String,
    var InterfaceName: String?,
    var Priority: String,
    var UseFlag: String,
    var RackID: String?,
    var WorkOrder: String?,
    var HaveFlag: String?,
    var BgnToEnd: String?,
    var MachineName: String?,
    var PutTime: String? =null,
): Parcelable {
    constructor(parcel: Parcel) : this(
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
    )

    override fun writeToParcel(parcel: Parcel, flags: Int) {
        parcel.writeString(Area)
        parcel.writeString(Block)
        parcel.writeString(Port)
        parcel.writeString(StationNo)
        parcel.writeString(InterfaceName)
        parcel.writeString(Priority)
        parcel.writeString(UseFlag)
        parcel.writeString(RackID)
        parcel.writeString(WorkOrder)
        parcel.writeString(HaveFlag)
        parcel.writeString(BgnToEnd)
        parcel.writeString(MachineName)
        parcel.writeString(PutTime)
    }

    override fun describeContents(): Int {
        return 0
    }

    companion object CREATOR : Parcelable.Creator<oPortModel> {
        override fun createFromParcel(parcel: Parcel): oPortModel {
            return oPortModel(parcel)
        }

        override fun newArray(size: Int): Array<oPortModel?> {
            return arrayOfNulls(size)
        }
    }
}