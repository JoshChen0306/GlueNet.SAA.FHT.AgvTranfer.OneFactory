package com.example.saa

import android.content.Intent
import android.os.Bundle
import android.widget.Button
import androidx.appcompat.app.AppCompatActivity
import com.example.saa.dispatch.DispatchActivityA
import com.example.saa.dispatch.DispatchActivityC
import com.example.saa.dispatch.DispatchActivityD
import com.example.saa.dispatch.DispatchActivityE

class OptionsActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_option)

        val buttonIds = listOf(
            R.id.buttonStationA to DispatchActivityA::class.java,
            R.id.buttonStationC to DispatchActivityC::class.java,
            R.id.buttonStationD to DispatchActivityD::class.java,
            R.id.buttonStationE to DispatchActivityE::class.java,
        )

        for ((buttonId, station) in buttonIds) {
            val button = findViewById<Button>(buttonId)
            button.setOnClickListener {
                val intent = Intent(this, station)
                startActivity(intent)
            }
        }
    }
}
