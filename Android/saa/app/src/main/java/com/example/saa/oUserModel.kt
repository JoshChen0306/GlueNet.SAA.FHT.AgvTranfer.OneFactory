package com.example.saa

import android.os.Parcel
import android.os.Parcelable

data class oUserModel(
    var userId: String?,
    var userName: String?,
    var password: String?,
    var groupId: String?,
    var mail: String?,
    var tel: String?,
    var modifiedTime: String?,
): Parcelable {
    constructor(parcel: Parcel) : this(
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
        parcel.readString().toString(),
    )

    override fun writeToParcel(parcel: Parcel, flags: Int) {
        parcel.writeString(userId)
        parcel.writeString(userName)
        parcel.writeString(password)
        parcel.writeString(groupId)
        parcel.writeString(mail)
        parcel.writeString(tel)
        parcel.writeString(modifiedTime)
    }

    override fun describeContents(): Int {
        return 0
    }

    companion object CREATOR : Parcelable.Creator<oUserModel> {
        override fun createFromParcel(parcel: Parcel): oUserModel {
            return oUserModel(parcel)
        }

        override fun newArray(size: Int): Array<oUserModel?> {
            return arrayOfNulls(size)
        }
    }
}