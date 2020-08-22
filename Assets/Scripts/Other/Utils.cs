// This class contains some helper functions.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

// some general UnityEvents
[Serializable] public class UnityEventString : UnityEvent<String> { }
public class Utils : MonoBehaviour
{
    // solo funciona para float e int. necesitamos algunas versiones más:
    public static long Clamp(long value, long min, long max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // ¿Alguna de las teclas está ARRIBA?
    public static bool AnyKeyUp(KeyCode[] keys)
    {
        // evite Linq.Any porque es PESADO (!) en GC y rendimiento
        foreach (KeyCode key in keys)
            if (Input.GetKeyUp(key))
                return true;
        return false;
    }

    // ¿Alguna de las teclas está ABAJO?
    public static bool AnyKeyDown(KeyCode[] keys)
    {
        // evite Linq.Any porque es PESADO (!) en GC y rendimiento
        foreach (KeyCode key in keys)
            if (Input.GetKeyDown(key))
                return true;
        return false;
    }

    // ¿Alguna de las teclas está PRESIONADA?
    public static bool AnyKeyPressed(KeyCode[] keys)
    {
        // evite Linq.Any porque es PESADO (!) en GC y rendimiento
        foreach (KeyCode key in keys)
            if (Input.GetKey(key))
                return true;
        return false;
    }

    // es un punto 2D en la pantalla?
    public static bool IsPointInScreen(Vector2 point)
    {
        return 0 <= point.x && point.x <= Screen.width &&
               0 <= point.y && point.y <= Screen.height;
    }

     // función auxiliar para calcular un radio de límites en el ESPACIO MUNDIAL
     // -> collider.radius es escala local
     // -> collider.bounds es a escala mundial
     // -> usa x + y extiende el promedio solo para estar seguro (para cápsulas, x == y extiende)
     // -> usa 'extiende' en lugar de 'tamaño' porque las extensiones son el radio.
     // en otras palabras: si venimos de la derecha, solo queremos detenernos en el radio, 
     // también conocido como la mitad del tamaño, no el doble del radio, también conocido como tamaño.
 
    public static float BoundsRadius(Bounds bounds) =>
        (bounds.extents.x + bounds.extents.z) / 2;


    // Distancia entre dos ClosestPoints esto es necesario en casos donde las entidades 
    // son realmente grandes. en esos casos, no podemos movernos a entity.transform.position,
    // porque será inalcanzable. en su lugar, tenemos que ir al punto más cercano en el límite.
    //
    // Vector3.Distance(a.transform.position, b.transform.position):
    //    _____        _____
    //   |     |      |     |
    //   |  x==|======|==x  |
    //   |_____|      |_____|
    //
    //
    // Utils.ClosestDistance(a.collider, b.collider):
    //    _____        _____
    //   |     |      |     |
    //   |     |x====x|     |
    //   |_____|      |_____|
    //
    // IMPORTANTE:
    //   Siempre pasamos Entity en lugar de Collider, porque entity.transform.position es 
    //   independiente de la animación, mientras que collider.transform.position cambia durante 
    //   las animaciones (las caderas).
    public static float ClosestDistance(Entity a, Entity b)
    {
        // IMPORTANTE: NO use el colisionador en sí. la posición cambia durante las animaciones, 
        //             lo que provoca situaciones en las que los ataques se interrumpen porque 
        //             las caderas del objetivo se movieron un poco fuera del rango de ataque, 
        //             ¡aunque el objetivo en realidad no se movió!

        //            => use transform.position and collider.radius instead!
        
        //             Esto es probablemente más rápido que el colisionador. Los puntos de cierre 
        //             también calculan primero la distancia de A a B, restan ambos radios
        // IMPORTANTE: use entity.transform.position no collider.transform.position. 
        //             ¡eso todavía sería la cadera!
        float distance = Vector3.Distance(a.transform.position, b.transform.position);

        // calcular el radio del colisionador
        float radiusA = BoundsRadius(a.collider.bounds);
        float radiusB = BoundsRadius(b.collider.bounds);

        //  resta ambos radios
        float distanceInside = distance - radiusA - radiusB;

        // distancia de retorno. si es <0 porque están uno dentro del otro, entonces
        // retorna 0.
        return Mathf.Max(distanceInside, 0);
    }

    // punto más cercano del colisionador de una entidad a otro punto, 
    // esto se usa en todo el lugar, así que vamos a colocarlo en un 
    // lugar para que sea más fácil modificar el método si es necesario
    public static Vector3 ClosestPoint(Entity entity, Vector3 point)
    {
        // IMPORTANTE: NO use el colisionador en sí. la posición cambia durante 
        // las animaciones, lo que provoca situaciones en las que los ataques se 
        // interrumpen porque las caderas del objetivo se movieron un poco fuera 
        // del rango de ataque, ¡aunque el objetivo en realidad no se movió!

        // => ¡use transform.position y collider.radius en su lugar!
        //
        // esto es probablemente más rápido que  collider.ClosestPoints

        // en primer lugar, obtenga el radio pero en el WORLD SPACE no en LOCAL SPACE.
        // de lo contrario, las escalas principales no se aplican.
        float radius = BoundsRadius(entity.collider.bounds);

        // ahora obtén la dirección del punto a la entidad
        // IMPORTANTE: use entity.transform.position no
        //             collider.transform.position. ¡eso todavía sería la cadera!
        Vector3 direction = entity.transform.position - point;
        //Debug.DrawLine(point, point + direction, Color.red, 1, false);

        // restar el radio de la longitud de la dirección
        Vector3 directionSubtracted = Vector3.ClampMagnitude(direction, direction.magnitude - radius);

        // Retorna el punto
        //Debug.DrawLine(point, point + directionSubtracted, Color.green, 1, false);
        return point + directionSubtracted;
    }

     // Las funciones CastWithout necesitan un diccionario de copias de seguridad. esto está en camino caliente
     // y crear un diccionario para cada llamada sería una locura.
    static Dictionary<Transform, int> castBackups = new Dictionary<Transform, int>();

    // Raycast mientras se ignora a sí mismo (configurando primero la capa "Ignorar Raycasts")
    // => configurar la capa como IgnoreRaycasts antes de lanzar es la forma más fácil de hacerlo
    // => raycast +! = esta comprobación aún provocaría que hit.point esté en el reproductor
    // => raycastall no está ordenado y los objetos secundarios pueden tener diferentes capas, etc.
    public static bool RaycastWithout(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, GameObject ignore, int layerMask = Physics.DefaultRaycastLayers)
    {
        // recuerda capas
        castBackups.Clear();

        // configura todo para ignorar raycast
        foreach (Transform tf in ignore.GetComponentsInChildren<Transform>(true))
        {
            castBackups[tf] = tf.gameObject.layer;
            tf.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        // raycast
        bool result = Physics.Raycast(origin, direction, out hit, maxDistance, layerMask);

        // restaurar capas
        foreach (KeyValuePair<Transform, int> kvp in castBackups)
            kvp.Key.gameObject.layer = kvp.Value;

        return result;
    }


    // calcular los límites de encapsulación de todos los renderizadores secundarios
    public static Bounds CalculateBoundsForAllRenderers(GameObject go)
    {
        Bounds bounds = new Bounds();
        bool initialized = false;
        foreach (Renderer rend in go.GetComponentsInChildren<Renderer>())
        {
            // inicializar o encapsular
            if (!initialized)
            {
                bounds = rend.bounds;
                initialized = true;
            }
            else bounds.Encapsulate(rend.bounds);
        }
        return bounds;
    }

    // función auxiliar para encontrar la transformación más cercana desde un punto 'desde'
    public static Transform GetNearestTransform(List<Transform> transforms, Vector3 from)
    {
        // nota: evite Linq para rendimiento / GC
        // => los jugadores pueden reaparecer con frecuencia, y el juego podría tener muchas 
        // posiciones de inicio, por lo que esta función es importante incluso si no está en ruta activa.
        Transform nearest = null;
        foreach (Transform tf in transforms)
        {
            // better candidate if we have no candidate yet, or if closer
            if (nearest == null ||
                Vector3.Distance(tf.position, from) < Vector3.Distance(nearest.position, from))
                nearest = tf;
        }
        return nearest;
    }

    // pretty print seconds as hours:minutes:seconds(.milliseconds/100)s
    public static string PrettySeconds(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        string res = "";
        if (t.Days > 0) res += t.Days + "d";
        if (t.Hours > 0) res += " " + t.Hours + "h";
        if (t.Minutes > 0) res += " " + t.Minutes + "m";
        // 0.5s, 1.5s etc. if any milliseconds. 1s, 2s etc. if any seconds
        if (t.Milliseconds > 0) res += " " + t.Seconds + "." + (t.Milliseconds / 100) + "s";
        else if (t.Seconds > 0) res += " " + t.Seconds + "s";
        // si la cadena sigue vacía porque el valor era '0', al menos devuelve los segundos
        // en lugar de devolver una cadena vacía

        return res != "" ? res : "0s";
    }

    // hard mouse scrolling that is consistent between all platforms
    //   Input.GetAxis("Mouse ScrollWheel") and
    //   Input.GetAxisRaw("Mouse ScrollWheel")
    //   both return values like 0.01 on standalone and 0.5 on WebGL, which
    //   causes too fast zooming on WebGL etc.
    // normally GetAxisRaw should return -1,0,1, but it doesn't for scrolling
    public static float GetAxisRawScrollUniversal()
    {
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll < 0) return -1;
        if (scroll > 0) return 1;
        return 0;
    }

    // two finger pinch detection
    // source: https://docs.unity3d.com/Manual/PlatformDependentCompilation.html
    public static float GetPinch()
    {
        if (Input.touchCount == 2)
        {
            // Almacene ambos toques.
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            // Encuentra la posición en el cuadro anterior de cada toque.
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            // Encuentra la magnitud del vector (la distancia) entre los toques en cada cuadro.
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            // Encuentra la diferencia en las distancias entre cada cuadro.
            return touchDeltaMag - prevTouchDeltaMag;
        }
        return 0;
    }

    // zoom universal: desplazamiento del mouse si el mouse, dos dedos pellizcando de lo contrario

    public static float GetZoomUniversal()
    {
        if (Input.mousePresent)
            return GetAxisRawScrollUniversal();
        else if (Input.touchSupported)
            return GetPinch();
        return 0;
    }

    // analiza el último sustantivo en mayúscula de una cadena, p.
    // EquipmentWeaponBow => Bow
    // EquipmentShield => Shield
    static Regex lastNountRegEx = new Regex(@"([A-Z][a-z]*)"); // caché para evitar asignaciones. Esto se usa mucho.
    public static string ParseLastNoun(string text)
    {
        MatchCollection matches = lastNountRegEx.Matches(text);
        return matches.Count > 0 ? matches[matches.Count - 1].Value : "";
    }

    // verifica si el cursor está sobre un elemento UI o OnGUI ahora mismo
    // nota: para la interfaz de usuario, esto solo funciona si CanvasGroup de la interfaz
    // de usuario bloquea los Raycasts
    // nota: para OnGUI: hotControl solo se establece al hacer clic, no al hacer zoom
    public static bool IsCursorOverUserInterface()
    {
        // IsPointerOverGameObject buscar el mouse izquierdo (predeterminado)
        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        // IsPointerOverGameObject verificar toques
        for (int i = 0; i < Input.touchCount; ++i)
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                return true;

        // Verificación de OnGUI
        return GUIUtility.hotControl != 0;
    }

    // hash PBKDF2 recomendado por NIST:
    // http://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-132.pdf
    // salt debe tener al menos 128 bits = 16 bytes
    public static string PBKDF2Hash(string text, string salt)
    {
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(text, saltBytes, 10000);
        byte[] hash = pbkdf2.GetBytes(20);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    // invocar múltiples funciones por prefijo a través de la reflexión.
    // -> también funciona para clases estáticas si object = null
    // -> lo almacena en caché para que sea lo suficientemente rápido para llamadas de actualización
    static Dictionary<KeyValuePair<Type, string>, MethodInfo[]> lookup = new Dictionary<KeyValuePair<Type, string>, MethodInfo[]>();
    public static MethodInfo[] GetMethodsByPrefix(Type type, string methodPrefix)
    {
        KeyValuePair<Type, string> key = new KeyValuePair<Type, string>(type, methodPrefix);
        if (!lookup.ContainsKey(key))
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                                       .Where(m => m.Name.StartsWith(methodPrefix))
                                       .ToArray();
            lookup[key] = methods;
        }
        return lookup[key];
    }

    public static void InvokeMany(Type type, object onObject, string methodPrefix, params object[] args)
    {
        foreach (MethodInfo method in GetMethodsByPrefix(type, methodPrefix))
            method.Invoke(onObject, args);
    }

     // sujeta una rotación alrededor del eje x
     // (por ejemplo, rotación arriba / abajo de la cámara para que no podamos mirar 
     // debajo de los pantalones del personaje, etc.)
     // fuente original: los activos estándar de Unity MouseLook.cs
    public static Quaternion ClampRotationAroundXAxis(Quaternion q, float min, float max)
    {
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1.0f;

        float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);
        angleX = Mathf.Clamp(angleX, min, max);
        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

        return q;
    }
}
