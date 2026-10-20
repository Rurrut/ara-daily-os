package ara.daily;
import android.Manifest;
import android.app.Activity;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.*;
import android.hardware.Camera;
import android.os.*;
import android.view.*;
import android.widget.*;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

public final class QrScannerActivity extends Activity implements SurfaceHolder.Callback,Camera.PreviewCallback {
 private SurfaceView surface;private TextView message;private Camera camera;private boolean resumed,finished;
 private final ExecutorService decode=Executors.newSingleThreadExecutor();private final AtomicBoolean decoding=new AtomicBoolean();private long lastFrame=0,lastNotice=0;
 private final Handler handler=new Handler(Looper.getMainLooper());
 @Override public void onCreate(Bundle state){super.onCreate(state);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);getWindow().setStatusBarColor(Color.rgb(16,24,21));getWindow().setNavigationBarColor(Color.rgb(16,24,21));
  LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setPadding(20,25,20,20);root.setBackgroundColor(Color.rgb(16,24,21));root.setOnApplyWindowInsetsListener((view,insets)->{if(Build.VERSION.SDK_INT>=30){Insets x=insets.getInsets(WindowInsets.Type.systemBars());root.setPadding(x.left+20,x.top+25,x.right+20,x.bottom+20);}else root.setPadding(insets.getSystemWindowInsetLeft()+20,insets.getSystemWindowInsetTop()+25,insets.getSystemWindowInsetRight()+20,insets.getSystemWindowInsetBottom()+20);return insets;});setContentView(root);
  TextView title=new TextView(this);title.setText("PC 연결용 QR 스캔");title.setTextColor(Color.WHITE);title.setTextSize(22);root.addView(title);
  message=new TextView(this);message.setText("PC 앱 → 휴대폰 연결 → 앱 연결용 QR을 비춰 주세요.\n카메라 영상은 저장하거나 전송하지 않습니다.");message.setTextColor(Color.rgb(185,210,195));message.setTextSize(15);message.setPadding(0,14,0,18);root.addView(message);
  FrameLayout frame=new FrameLayout(this);root.addView(frame,new LinearLayout.LayoutParams(-1,0,1));surface=new SurfaceView(this);surface.getHolder().addCallback(this);frame.addView(surface,new FrameLayout.LayoutParams(-1,-1));frame.addView(new Guide(this),new FrameLayout.LayoutParams(-1,-1));
  Button cancel=new Button(this);cancel.setText("취소 · 직접 입력으로 돌아가기");cancel.setOnClickListener(v->finish());root.addView(cancel,new LinearLayout.LayoutParams(-1,-2));
  if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.CAMERA},71);
 }
 @Override protected void onResume(){super.onResume();resumed=true;startCamera();}
 @Override protected void onPause(){resumed=false;stopCamera();super.onPause();}
 @Override protected void onDestroy(){finished=true;stopCamera();decode.shutdownNow();super.onDestroy();}
 @Override public void onRequestPermissionsResult(int request,String[] permissions,int[] grants){super.onRequestPermissionsResult(request,permissions,grants);if(request==71){if(grants.length>0&&grants[0]==PackageManager.PERMISSION_GRANTED)startCamera();else{message.setText("카메라 권한이 필요합니다. 취소 후 주소와 연결 코드를 직접 입력할 수도 있습니다.");}}}
 public void surfaceCreated(SurfaceHolder holder){startCamera();}public void surfaceChanged(SurfaceHolder holder,int format,int width,int height){startCamera();}public void surfaceDestroyed(SurfaceHolder holder){stopCamera();}
 private void startCamera(){if(camera!=null||!resumed||finished||surface==null||!surface.getHolder().getSurface().isValid()||checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)return;
  try{int id=-1;Camera.CameraInfo info=new Camera.CameraInfo();for(int i=0;i<Camera.getNumberOfCameras();i++){Camera.getCameraInfo(i,info);if(info.facing==Camera.CameraInfo.CAMERA_FACING_BACK){id=i;break;}}if(id<0)throw new Exception();Camera.CameraInfo selected=new Camera.CameraInfo();Camera.getCameraInfo(id,selected);camera=Camera.open(id);Camera.Parameters params=camera.getParameters();params.setPreviewFormat(ImageFormat.NV21);Camera.Size size=null;for(Camera.Size s:params.getSupportedPreviewSizes()){if(s.width==640&&s.height==480){size=s;break;}if(s.width<=1280&&s.height<=720&&(size==null||s.width*s.height>size.width*size.height))size=s;}if(size!=null)params.setPreviewSize(size.width,size.height);List<String> focus=params.getSupportedFocusModes();if(focus!=null&&focus.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))params.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);else if(focus!=null&&focus.contains(Camera.Parameters.FOCUS_MODE_AUTO))params.setFocusMode(Camera.Parameters.FOCUS_MODE_AUTO);camera.setParameters(params);int degrees=getWindowManager().getDefaultDisplay().getRotation()*90;camera.setDisplayOrientation((selected.orientation-degrees+360)%360);camera.setPreviewDisplay(surface.getHolder());camera.setPreviewCallback(this);camera.startPreview();focusAgain();
  }catch(Exception error){stopCamera();message.setText("카메라를 열지 못했습니다. 다른 카메라 앱을 닫고 다시 시도하거나 직접 입력으로 연결해 주세요.");}}
 private void focusAgain(){handler.removeCallbacks(refocus);handler.postDelayed(refocus,1600);}
 private final Runnable refocus=new Runnable(){public void run(){if(camera==null||!resumed)return;try{if(Camera.Parameters.FOCUS_MODE_AUTO.equals(camera.getParameters().getFocusMode()))camera.autoFocus((ok,c)->{});}catch(Exception ignored){}focusAgain();}};
 private void stopCamera(){handler.removeCallbacks(refocus);if(camera!=null){try{camera.setPreviewCallback(null);camera.stopPreview();}catch(Exception ignored){}try{camera.release();}catch(Exception ignored){}camera=null;}}
 public void onPreviewFrame(byte[] data,Camera source){if(data==null||!resumed||finished||System.currentTimeMillis()-lastFrame<180||!decoding.compareAndSet(false,true))return;lastFrame=System.currentTimeMillis();Camera.Size size;try{size=source.getParameters().getPreviewSize();}catch(Exception error){decoding.set(false);return;}final int width=size.width,height=size.height;final byte[] frame=Arrays.copyOf(data,data.length);
  try{decode.execute(()->{try{PlanarYUVLuminanceSource pixels=new PlanarYUVLuminanceSource(frame,width,height,0,0,width,height,false);Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);hints.put(DecodeHintType.TRY_HARDER,Boolean.TRUE);Result result=new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(pixels)),hints);QrPayload payload=QrPayload.parse(result.getText());runOnUiThread(()->{if(!resumed||finished)return;finished=true;stopCamera();Intent output=new Intent();output.putExtra("pc",payload.pc);output.putExtra("code",payload.code);setResult(RESULT_OK,output);finish();});
   }catch(NotFoundException ignored){}catch(ChecksumException ignored){}catch(FormatException ignored){}catch(Exception error){long now=System.currentTimeMillis();if(now-lastNotice>1800){lastNotice=now;runOnUiThread(()->{if(resumed&&!finished)message.setText("PC 앱의 ‘앱 연결용 QR’을 비춰 주세요. 설치 페이지 QR은 연결에 사용할 수 없습니다.");});}}finally{decoding.set(false);}});}catch(RejectedExecutionException ignored){decoding.set(false);}
 }
 static final class Guide extends View {final Paint paint=new Paint(3);Guide(Context c){super(c);setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);}protected void onDraw(Canvas canvas){float side=Math.min(getWidth(),getHeight())*.78f,left=(getWidth()-side)/2,top=(getHeight()-side)/2;paint.setColor(Color.rgb(105,206,154));paint.setStrokeWidth(5);paint.setStyle(Paint.Style.STROKE);canvas.drawRoundRect(left,top,left+side,top+side,20,20,paint);}}
}
