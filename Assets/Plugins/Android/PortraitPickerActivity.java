package com.endlessreincarnation.mobile;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.Locale;

public final class PortraitPickerActivity extends Activity {
    private static final int REQUEST_PICK_IMAGE = 4107;
    private static final long MAX_IMAGE_BYTES = 32L * 1024L * 1024L;
    private static final String UNITY_OBJECT_NAME = "PlayerProfileAndroidFilePicker";
    private static final String UNITY_CALLBACK_NAME = "OnAndroidPortraitPicked";

    public static void Open(final Activity sourceActivity) {
        if (sourceActivity == null) {
            sendResult("ERROR:NO_ACTIVITY");
            return;
        }

        sourceActivity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                Intent pickerActivity = new Intent(sourceActivity, PortraitPickerActivity.class);
                sourceActivity.startActivity(pickerActivity);
            }
        });
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState == null) {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("image/*");
            intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] { "image/png", "image/jpeg" });
            startActivityForResult(intent, REQUEST_PICK_IMAGE);
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_PICK_IMAGE)
            return;

        String result = "";
        if (resultCode == RESULT_OK && data != null && data.getData() != null) {
            try {
                result = copySelectedImage(data.getData());
            } catch (UnsupportedImageException ex) {
                result = "ERROR:UNSUPPORTED_FORMAT";
            } catch (ImageTooLargeException ex) {
                result = "ERROR:FILE_TOO_LARGE";
            } catch (Exception ex) {
                result = "ERROR:READ_FAILED";
            }
        }

        sendResult(result);
        finish();
    }

    private String copySelectedImage(Uri uri)
            throws IOException, UnsupportedImageException, ImageTooLargeException {
        ContentResolver resolver = getContentResolver();
        String extension = resolveExtension(resolver, uri);
        if (extension == null)
            throw new UnsupportedImageException();

        File cacheDirectory = new File(getCacheDir(), "portrait-picker");
        if (!cacheDirectory.exists() && !cacheDirectory.mkdirs())
            throw new IOException("Unable to create cache directory");

        File outputFile = File.createTempFile("portrait_", extension, cacheDirectory);
        boolean copied = false;
        try (InputStream input = resolver.openInputStream(uri);
             FileOutputStream output = new FileOutputStream(outputFile)) {
            if (input == null)
                throw new IOException("Unable to open selected image");

            byte[] buffer = new byte[8192];
            long totalBytes = 0L;
            int read;
            while ((read = input.read(buffer)) != -1) {
                totalBytes += read;
                if (totalBytes > MAX_IMAGE_BYTES)
                    throw new ImageTooLargeException();
                output.write(buffer, 0, read);
            }
            copied = true;
        } finally {
            if (!copied)
                outputFile.delete();
        }

        return outputFile.getAbsolutePath();
    }

    private static String resolveExtension(ContentResolver resolver, Uri uri) {
        String mimeType = resolver.getType(uri);
        if ("image/png".equalsIgnoreCase(mimeType))
            return ".png";
        if ("image/jpeg".equalsIgnoreCase(mimeType) || "image/jpg".equalsIgnoreCase(mimeType))
            return ".jpg";

        String displayName = queryDisplayName(resolver, uri).toLowerCase(Locale.ROOT);
        if (displayName.endsWith(".png"))
            return ".png";
        if (displayName.endsWith(".jpg") || displayName.endsWith(".jpeg"))
            return ".jpg";
        return null;
    }

    private static String queryDisplayName(ContentResolver resolver, Uri uri) {
        try (Cursor cursor = resolver.query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (index >= 0) {
                    String value = cursor.getString(index);
                    return value != null ? value : "";
                }
            }
        } catch (Exception ignored) {
        }
        return "";
    }

    private static void sendResult(String result) {
        UnityPlayer.UnitySendMessage(UNITY_OBJECT_NAME, UNITY_CALLBACK_NAME, result != null ? result : "");
    }

    private static final class UnsupportedImageException extends Exception {
    }

    private static final class ImageTooLargeException extends Exception {
    }
}
