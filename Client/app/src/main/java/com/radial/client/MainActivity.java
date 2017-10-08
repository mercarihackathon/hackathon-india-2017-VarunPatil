package com.radial.client;

import android.Manifest;
import android.app.Activity;
import android.app.PendingIntent;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.net.Uri;
import android.nfc.NfcAdapter;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;
import android.support.v13.app.ActivityCompat;
import android.support.v4.content.ContextCompat;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.Switch;
import android.widget.Toast;

import java.io.BufferedReader;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.URL;
import java.net.URLConnection;
import java.util.Scanner;
import java.util.Timer;
import java.util.TimerTask;

import static android.provider.AlarmClock.EXTRA_MESSAGE;
import android.content.Intent;
import android.support.v7.app.AppCompatActivity;
import android.os.Bundle;
import android.view.View;
import android.widget.EditText;

public class MainActivity extends Activity {
    /* Defaults and statics */
    public static final String PREFS_NAME = "TCPClientConf";
    public static final int DEFAULT_QUALITY = 80;
    public static final int DEFAULT_DIM = 512;
    public static final int DEFAULT_PORT = 3800;
    private static final int CAMERA_REQUEST = 1888;
    private static final int BUFFER_SIZE = 1024;
    public static final String FILE_STORAGE_DIR = Environment.getExternalStorageDirectory().getAbsolutePath() + "/TCPClient/";
    private static final String V_ID = "4";
    private boolean Unused_Currently = true;

    /* Declare Controls */
    private EditText IPEditText;
    private EditText PortEditText;
    private Button retryButton;

    private Uri mImageUri;
    private static Boolean sharedFile = false;

    private Double currentLat = 19.104084;
    private Double currentLng = 72.871368;

    private NfcAdapter mNfcAdapter;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.main);

        if (Build.VERSION.SDK_INT >= 23) {
            if (ContextCompat.checkSelfPermission(MainActivity.this, Manifest.permission.WRITE_EXTERNAL_STORAGE) != PackageManager.PERMISSION_GRANTED) {
                ActivityCompat.requestPermissions(MainActivity.this, new String[]{android.Manifest.permission.WRITE_EXTERNAL_STORAGE}, 1);
            }
        }

        mNfcAdapter = NfcAdapter.getDefaultAdapter(this);

        if (mNfcAdapter == null) {
            Toast.makeText(this, "This device doesn't support NFC.", Toast.LENGTH_LONG).show();
        } else {
            if (!mNfcAdapter.isEnabled()) {
                Toast.makeText(this, "NFC Disabled", Toast.LENGTH_SHORT).show();
            } else {

            }
        }

        /* Initialize Controls */
        Button photoButton = (Button) this.findViewById(R.id.btnPhoto);
        Button saveButton = (Button) this.findViewById(R.id.btnSave);
        Button rideButton = (Button) this.findViewById(R.id.btnRide);
        Button endrideButton = (Button) this.findViewById(R.id.btnEndRide);
        retryButton = (Button) this.findViewById(R.id.btnRetry);

        IPEditText = (EditText)findViewById(R.id.txtip);
        PortEditText = (EditText)findViewById(R.id.txtport);

        /* Load Preferences */
        final SharedPreferences settings = getSharedPreferences(PREFS_NAME, 0);     
        IPEditText.setText(settings.getString("IP", ""));
        PortEditText.setText(Integer.toString(settings.getInt("Port", DEFAULT_PORT)));

        Timer timer = new Timer();
        timer.schedule(new UpdateLocation(), 0, 1000);

        photoButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                new pingAsync().execute("startuse?id=" + V_ID + "&user=200");
                Unused_Currently = false;
            }
        });

        rideButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                currentLat += 0.0001; currentLng -= 0.00012;
                new pingAsync().execute("updateloc?id=" + V_ID + "&lat=" + currentLat.toString() + "&lng=" + currentLng.toString() + "&unused=" + (Unused_Currently?"1":"0"));
            }
        });

        endrideButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                new pingAsync().execute("enduse?id=" + V_ID + "&user=200");
                Unused_Currently = true;
            }
        });

        saveButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) { savePreferences();

            }
        });

        retryButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent = new Intent(MainActivity.this, MapsActivity.class);
                String message = "BLAH";

                //final String XTRA_MESSAGE = "com.example.myfirstapp.MESSAGE";
                //intent.putExtra(XTRA_MESSAGE, message);
                startActivity(intent);
            }
        });
    }

    private void savePreferences()
    {
        SharedPreferences settings = getSharedPreferences(PREFS_NAME, 0);
        SharedPreferences.Editor editor = settings.edit();
        try{
                    /* Check for erroneous values */;
            int Port=Integer.parseInt(PortEditText.getText().toString());
            if (Port <= 0 || Port >= 65534) throw new Exception("Invalid Port");
            /* Save Preferences */
            editor.putInt("Port", Port);
            editor.putString("IP", IPEditText.getText().toString());
        }
        catch(Exception e){
            Toast.makeText(getApplicationContext(),e.getMessage(),Toast.LENGTH_SHORT).show();
            return;
        }
                /* Commit the edits! */
        editor.commit();
        Toast.makeText(getApplicationContext(), "Preferences Saved", Toast.LENGTH_SHORT).show();
    }
    
    public boolean runJavaSocket(String str) {
        /* Load Preferences */
        SharedPreferences settings = getSharedPreferences(PREFS_NAME, 0);
        final String IP = settings.getString("IP", "");

        byte[] buffer = new byte[BUFFER_SIZE];

        try{
            /* Open a connection */
            Socket socket = new Socket(InetAddress.getByName(IP), settings.getInt("Port", DEFAULT_PORT));
    
            /* Write everything */
            OutputStream output = socket.getOutputStream();
            
            String string = "HEADER\n" + Integer.toString(str.length()) + "\n" + str;
            System.arraycopy(string.getBytes("US-ASCII"), 0, buffer, 0, string.length());
            
            output.write(buffer);
            /* Flush the output to commit */
            output.flush();

            return true;
        }
        catch (Exception e){
            Log.e("Client", "exception", e);
            return false;
        }
    }
    
    /* Check if IP:Port is open */
    public boolean serverListening(String host, int port, boolean prompt)
    {
        Socket s = new Socket();
        try
        {
            s.connect(new InetSocketAddress(host, port), 1000);
            return true;
        }
        catch (Exception e)
        {
            if (prompt) Toast.makeText(getApplicationContext(),e.getMessage(),Toast.LENGTH_SHORT).show();
            return false;
        }
        finally
        {
            try {s.close();}
            catch(Exception ignored){}
        }
    }

    public boolean serverListening(String host, int port){
        return serverListening(host, port, false);
    }

    private class pingAsync extends AsyncTask<String,Integer,Boolean>{
        @Override
        protected Boolean doInBackground(String[] string) {
            String content = pingServer(string[0]);
            return true;
        }
    }

    //https://stackoverflow.com/questions/4328711/read-url-to-string-in-few-lines-of-java-code
    public String pingServer(String conditions) {
        try {
            SharedPreferences settings = getSharedPreferences(PREFS_NAME, 0);
            final String IP = settings.getString("IP", "");
            final String PORT = new Integer(settings.getInt("Port", DEFAULT_PORT)).toString();
            URL website = new URL("http://" + IP + ":" + PORT + "/" + conditions);
            URLConnection connection = website.openConnection();
            BufferedReader in = new BufferedReader(
                    new InputStreamReader(
                            connection.getInputStream()));

            StringBuilder response = new StringBuilder();
            String inputLine;

            while ((inputLine = in.readLine()) != null)
                response.append(inputLine);

            in.close();
            return response.toString();
        } catch (Exception ignored) {
            return "NOTHING";
        }
    }

    private class SendAsync extends AsyncTask<String,Integer,Boolean>{
        @Override
        protected Boolean doInBackground(String[] string) {
            return runJavaSocket(string[0]);
        }

        @Override
        protected void onPostExecute(Boolean result) {
            Toast.makeText(getApplicationContext(), "File sending " + (result ? "" : "un") + "successful", Toast.LENGTH_SHORT).show();
        }
    }

    public void setupForegroundDispatch(final Activity activity, NfcAdapter adapter) {
        final Intent intent = new Intent(activity, activity.getClass());
        intent.putExtra("methodName","NFCDispatch");
        intent.setFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);

        final PendingIntent pendingIntent = PendingIntent.getActivity(activity, 0, intent, 0);

        adapter.enableForegroundDispatch(activity, pendingIntent, null, null);
    }

    @Override
    protected void onResume() {
        super.onResume();

        setupForegroundDispatch(this, mNfcAdapter);
    }

    @Override
    protected void onPause() {

        stopForegroundDispatch(this, mNfcAdapter);

        super.onPause();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        handleIntent(intent);
    }

    private void handleIntent(Intent intent)
    {
        if(intent.getStringExtra("methodName").equals("NFCDispatch")){
            Toast.makeText(getApplicationContext(), "Starting your trip!", Toast.LENGTH_SHORT).show();
            new pingAsync().execute("startuse?id=" + V_ID + "&user=200");
            Unused_Currently = false;
        }
    }

    public static void stopForegroundDispatch(final Activity activity, NfcAdapter adapter) {
        adapter.disableForegroundDispatch(activity);
    }

    class UpdateLocation extends TimerTask {
        public void run() {
            pingServer("updateloc?id=" + V_ID + "&lat=" + currentLat.toString() + "&lng=" + currentLng.toString() + "&unused=" + (Unused_Currently?"1":"0"));
        }
    }

}
