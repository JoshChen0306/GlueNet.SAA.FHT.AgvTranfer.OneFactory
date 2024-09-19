package com.example.saa

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.CheckBox
import android.widget.EditText
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : ComponentActivity() {
    private lateinit var etUsername: EditText
    private lateinit var edPassword: EditText
    private lateinit var btnLogin: Button
    private lateinit var cbRememberMe : CheckBox
    private lateinit var connectionStatus: View
    private lateinit var databaseHelper: DatabaseHelper

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_login)

        etUsername = findViewById(R.id.etUsername)
        edPassword = findViewById(R.id.etPassword)
        btnLogin = findViewById(R.id.btnLogin)
        connectionStatus = findViewById(R.id.connectionStatus)
        databaseHelper = DatabaseHelper()
        cbRememberMe=findViewById(R.id.cbRememberMe )

        checkConnectionStatus()

        val sharedPreferences = getSharedPreferences("LoginPrefs", Context.MODE_PRIVATE)
        val rememberedUsername = sharedPreferences.getString("username", "")

        if (rememberedUsername!!.isNotEmpty()) {
            etUsername.setText(rememberedUsername)
            cbRememberMe.isChecked = true
        }

        btnLogin.setOnClickListener {
            val username = etUsername.text.toString()
            val password = etUsername.text.toString()

            lifecycleScope.launch {
                if (withContext(Dispatchers.IO) { databaseHelper.checkDatabaseConnection() }) {
                    val isSuccess = withContext(Dispatchers.IO) { databaseHelper.checkLogin(username, password) }
                    if (isSuccess) {
                        if (cbRememberMe.isChecked) {
                            with(sharedPreferences.edit()) {
                                putString("username", username)
                                apply()
                            }
                        } else {
                            with(sharedPreferences.edit()) {
                                remove("username")
                                apply()
                            }
                        }
                        val intent = Intent(this@MainActivity, OptionsActivity::class.java)
                        startActivity(intent)
                        Toast.makeText(this@MainActivity, "Login sucessful.", Toast.LENGTH_SHORT).show()
                    } else {
                        Toast.makeText(this@MainActivity, "Login fail.", Toast.LENGTH_SHORT).show()
                    }
                } else {
                    Toast.makeText(this@MainActivity, "SQL connected error.", Toast.LENGTH_SHORT).show()
                }
            }
        }
    }

    private fun checkConnectionStatus() {
        lifecycleScope.launch {
            val isConnected = withContext(Dispatchers.IO) { databaseHelper.checkDatabaseConnection() }
            if (isConnected) {
                connectionStatus.setBackgroundResource(R.drawable.status_online)
            } else {
                connectionStatus.setBackgroundResource(R.drawable.status_offline)
            }
        }
    }
}
