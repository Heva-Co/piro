package co.heva.piro.shared.auth

import kotlinx.cinterop.BetaInteropApi
import kotlinx.cinterop.ExperimentalForeignApi
import kotlinx.cinterop.alloc
import kotlinx.cinterop.allocArrayOf
import kotlinx.cinterop.memScoped
import kotlinx.cinterop.ptr
import kotlinx.cinterop.reinterpret
import kotlinx.cinterop.value
import platform.CoreFoundation.CFDictionaryCreate
import platform.CoreFoundation.CFDictionaryRef
import platform.CoreFoundation.CFRelease
import platform.CoreFoundation.CFStringRef
import platform.CoreFoundation.CFTypeRef
import platform.CoreFoundation.CFTypeRefVar
import platform.CoreFoundation.kCFAllocatorDefault
import platform.CoreFoundation.kCFBooleanTrue
import platform.CoreFoundation.kCFTypeDictionaryKeyCallBacks
import platform.CoreFoundation.kCFTypeDictionaryValueCallBacks
import platform.Foundation.CFBridgingRelease
import platform.Foundation.CFBridgingRetain
import platform.Foundation.NSData
import platform.Foundation.NSString
import platform.Foundation.NSUTF8StringEncoding
import platform.Foundation.create
import platform.Foundation.dataUsingEncoding
import platform.Security.SecItemAdd
import platform.Security.SecItemCopyMatching
import platform.Security.SecItemDelete
import platform.Security.errSecSuccess
import platform.Security.kSecAttrAccessible
import platform.Security.kSecAttrAccessibleAfterFirstUnlock
import platform.Security.kSecAttrAccount
import platform.Security.kSecAttrService
import platform.Security.kSecClass
import platform.Security.kSecClassGenericPassword
import platform.Security.kSecMatchLimit
import platform.Security.kSecMatchLimitOne
import platform.Security.kSecReturnData
import platform.Security.kSecValueData

/**
 * iOS [TokenStorage] backed by the system Keychain (Security framework) — the counterpart to Android's
 * Keystore-encrypted preferences. Tokens survive app restarts and are stored with
 * `kSecAttrAccessibleAfterFirstUnlock`, so a background push handler launched after first unlock can
 * still read them to authorize an acknowledge. Each token is a separate generic-password item keyed by
 * [SERVICE] + account name.
 */
@OptIn(ExperimentalForeignApi::class, BetaInteropApi::class)
class KeychainTokenStorage : TokenStorage {

    override var accessToken: String?
        get() = read(ACCOUNT_ACCESS)
        set(value) = write(ACCOUNT_ACCESS, value)

    override var refreshToken: String?
        get() = read(ACCOUNT_REFRESH)
        set(value) = write(ACCOUNT_REFRESH, value)

    override fun clear() {
        delete(ACCOUNT_ACCESS)
        delete(ACCOUNT_REFRESH)
    }

    /** Overwrite semantics: remove any existing item, then add the new value (a null clears the slot). */
    private fun write(account: String, value: String?) {
        delete(account)
        if (value == null) return

        val data = (value as NSString).dataUsingEncoding(NSUTF8StringEncoding) ?: return
        val service = CFBridgingRetain(SERVICE as NSString)
        val acc = CFBridgingRetain(account as NSString)
        val dataRef = CFBridgingRetain(data)
        val query = cfDictionaryOf(
            kSecClass to kSecClassGenericPassword,
            kSecAttrService to service,
            kSecAttrAccount to acc,
            kSecValueData to dataRef,
            kSecAttrAccessible to kSecAttrAccessibleAfterFirstUnlock,
        )
        SecItemAdd(query, null)
        CFRelease(query)
        CFRelease(service)
        CFRelease(acc)
        CFRelease(dataRef)
    }

    private fun read(account: String): String? = memScoped {
        val service = CFBridgingRetain(SERVICE as NSString)
        val acc = CFBridgingRetain(account as NSString)
        val query = cfDictionaryOf(
            kSecClass to kSecClassGenericPassword,
            kSecAttrService to service,
            kSecAttrAccount to acc,
            kSecReturnData to kCFBooleanTrue,
            kSecMatchLimit to kSecMatchLimitOne,
        )
        val result = alloc<CFTypeRefVar>()
        val status = SecItemCopyMatching(query, result.ptr)
        CFRelease(query)
        CFRelease(service)
        CFRelease(acc)

        if (status != errSecSuccess) return@memScoped null
        val nsData = CFBridgingRelease(result.value) as? NSData ?: return@memScoped null
        NSString.create(nsData, NSUTF8StringEncoding) as String?
    }

    private fun delete(account: String) {
        val service = CFBridgingRetain(SERVICE as NSString)
        val acc = CFBridgingRetain(account as NSString)
        val query = cfDictionaryOf(
            kSecClass to kSecClassGenericPassword,
            kSecAttrService to service,
            kSecAttrAccount to acc,
        )
        SecItemDelete(query)
        CFRelease(query)
        CFRelease(service)
        CFRelease(acc)
    }

    /**
     * Creates a CFDictionary from the given (key, value) pairs. CFDictionaryCreate copies the entries
     * and, via the type callbacks, retains the values — so the transient C arrays can be freed with the
     * enclosing [memScoped], and the caller still owns (and must release) any bridged values it passed.
     */
    private fun cfDictionaryOf(vararg pairs: Pair<CFStringRef?, CFTypeRef?>): CFDictionaryRef = memScoped {
        val keys = allocArrayOf(pairs.map { it.first })
        val values = allocArrayOf(pairs.map { it.second })
        CFDictionaryCreate(
            kCFAllocatorDefault,
            keys.reinterpret(),
            values.reinterpret(),
            pairs.size.toLong(),
            kCFTypeDictionaryKeyCallBacks.ptr,
            kCFTypeDictionaryValueCallBacks.ptr,
        )!!
    }

    private companion object {
        const val SERVICE = "co.heva.piro.tokens"
        const val ACCOUNT_ACCESS = "accessToken"
        const val ACCOUNT_REFRESH = "refreshToken"
    }
}
