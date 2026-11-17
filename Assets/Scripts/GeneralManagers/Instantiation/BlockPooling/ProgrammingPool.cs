using UnityEngine;
using UnityEngine.Pool; // Necessário para usar o sistema nativo
using System.Collections.Generic;

public class ProgrammingPool : MonoBehaviour
{
    [System.Serializable]
    public struct BlockNextPoolConfig {
        public BlockUINext prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct BlockBodyPoolConfig {
        public BlockUIBody prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct DeclareFunctionPoolConfig {
        public FunctionDeclareUI prefab;
        public int defaultCapacity;
        public int maxSize;
    }

    [System.Serializable]
    public struct CallFunctionPoolConfig {
        public CallFunctionUI prefab;
        public int defaultCapacity;
        public int maxSize;
    }

    [System.Serializable]
    public struct CreateVarPoolConfig {
        public CreateVariableUI prefab;
        public int defaultCapacity;
        public int maxSize;    
    }

    [System.Serializable]
    public struct ImportBlockPoolConfig {
        public ImportBlockUI prefab;
        public int defaultCapacity;
        public int maxSize;
    }

    [System.Serializable]
    public struct ParamPoolConfig {
        public ReferenceHolder prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct SectionPoolConfig {
        public SectionInfo prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct ValuePoolConfig {
        public ValueInfo prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    public BlocksFactory blocksFactory;

    [SerializeField] public BlockNextPoolConfig blockNextConfig;
    [SerializeField] public BlockBodyPoolConfig blockBodyConfig;
    [SerializeField] public BlockNextPoolConfig blockVarConfig;
    [SerializeField] public DeclareFunctionPoolConfig declareFunctionConfig;
    [SerializeField] public CallFunctionPoolConfig callFunctionConfig;
    [SerializeField] public CreateVarPoolConfig createVarConfig;
    [SerializeField] public ImportBlockPoolConfig importBlockConfig;
    [SerializeField] public ParamPoolConfig parameterConfig;
    [SerializeField] public SectionPoolConfig sectionConfig;
    [SerializeField] public ValuePoolConfig valueConfig;

    private ObjectPool<BlockUINext> blockNextPool;
    private ObjectPool<BlockUIBody> blockBodyPool;
    private ObjectPool<BlockUINext> blockVarPool;
    private ObjectPool<FunctionDeclareUI> declareFunctionPool;
    private ObjectPool<CallFunctionUI> callFunctionPool;
    private ObjectPool<CreateVariableUI> createVarPool;
    private ObjectPool<ImportBlockUI> importBlockPool;
    private ObjectPool<ReferenceHolder> parameterPool;
    private ObjectPool<SectionInfo> sectionPool;
    private ObjectPool<ValueInfo> valuePool;

    void Awake()
    {
        
        blockNextPool = new ObjectPool<BlockUINext>(
            createFunc: () => Instantiate(blockNextConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockNextConfig.defaultCapacity, 
            blockNextConfig.maxSize
        );

        blockBodyPool = new ObjectPool<BlockUIBody>(
            createFunc: () => Instantiate(blockBodyConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockBodyConfig.defaultCapacity, 
            blockBodyConfig.maxSize
        );

        blockVarPool = new ObjectPool<BlockUINext>(
            createFunc: () => Instantiate(blockVarConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockVarConfig.defaultCapacity, 
            blockVarConfig.maxSize
        );

        declareFunctionPool = new ObjectPool<FunctionDeclareUI>(
            createFunc: () => Instantiate(declareFunctionConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            declareFunctionConfig.defaultCapacity, 
            declareFunctionConfig.maxSize
        );

        callFunctionPool = new ObjectPool<CallFunctionUI>(
            createFunc: () => Instantiate(callFunctionConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            callFunctionConfig.defaultCapacity, 
            callFunctionConfig.maxSize
        );

        createVarPool = new ObjectPool<CreateVariableUI>(
            createFunc: () => Instantiate(createVarConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            createVarConfig.defaultCapacity, 
            createVarConfig.maxSize
        );

        // Pool do bloco de import: só é criado se o prefab existir (evita erro na inicialização
        // enquanto o prefab ainda não foi criado no editor).
        if (importBlockConfig.prefab != null) {
            importBlockPool = new ObjectPool<ImportBlockUI>(
                createFunc: () => Instantiate(importBlockConfig.prefab, this.transform),
                actionOnGet: go => go.gameObject.SetActive(true),
                actionOnRelease: go => go.gameObject.SetActive(false),
                actionOnDestroy: go => Destroy(go),
                collectionCheck: true,
                importBlockConfig.defaultCapacity,
                importBlockConfig.maxSize
            );
        }

        parameterPool = new ObjectPool<ReferenceHolder>(
            createFunc: () => Instantiate(parameterConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            parameterConfig.defaultCapacity, 
            parameterConfig.maxSize
        );

        sectionPool = new ObjectPool<SectionInfo>(
            createFunc: () => Instantiate(sectionConfig.prefab, this.transform), 
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            sectionConfig.defaultCapacity, 
            sectionConfig.maxSize
        );

        valuePool = new ObjectPool<ValueInfo>(
            createFunc: () => Instantiate(valueConfig.prefab, this.transform), 
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            valueConfig.defaultCapacity, 
            valueConfig.maxSize
        );
    }

    public BlockUINext GetBlockNext() => blockNextPool.Get();
    public void ReleaseBlockNext(BlockUINext obj) {
        obj.transform.SetParent(this.transform, false);
        blockNextPool.Release(obj);
    }

    public BlockUIBody GetBlockBody() => blockBodyPool.Get();
    public void ReleaseBlockBody(BlockUIBody obj) {
        obj.transform.SetParent(this.transform, false);
        blockBodyPool.Release(obj);
    } 

    public BlockUINext GetBlockVar() => blockVarPool.Get();
    public void ReleaseBlockVar(BlockUINext obj) {
        obj.transform.SetParent(this.transform, false);
        blockVarPool.Release(obj);
    }

    public FunctionDeclareUI GetDeclareFunction() => declareFunctionPool != null ? declareFunctionPool.Get() : null;
    public void ReleaseDeclareFunction(FunctionDeclareUI obj) {
        if (declareFunctionPool == null || obj == null) return;
        obj.transform.SetParent(this.transform, false);
        declareFunctionPool.Release(obj);
    }

    public CallFunctionUI GetCallFunction() => callFunctionPool != null ? callFunctionPool.Get() : null;
    public void ReleaseCallFunction(CallFunctionUI obj) {
        if (callFunctionPool == null || obj == null) return;
        obj.transform.SetParent(this.transform, false);
        callFunctionPool.Release(obj);
    }

    public CreateVariableUI GetCreateVar() => createVarPool != null ? createVarPool.Get() : null;
    public void ReleaseCreateVar(CreateVariableUI obj) {
        if (createVarPool == null || obj == null) return;
        obj.transform.SetParent(this.transform, false);
        createVarPool.Release(obj);
    }

    public ImportBlockUI GetImportBlock() => importBlockPool != null ? importBlockPool.Get() : null;
    public void ReleaseImportBlock(ImportBlockUI obj) {
        if (importBlockPool == null || obj == null) return;
        obj.transform.SetParent(this.transform, false);
        importBlockPool.Release(obj);
    }

    public ReferenceHolder GetParameter() => parameterPool.Get();
    public void ReleaseParameter(ReferenceHolder obj) {
        obj.transform.SetParent(this.transform, false);
        parameterPool.Release(obj);
    } 

    public SectionInfo GetSection() => sectionPool.Get();
    public void ReleaseSection(SectionInfo obj) {
        obj.transform.SetParent(this.transform, false);
        sectionPool.Release(obj);
    } 

    public ValueInfo GetValue() => valuePool.Get();
    public void ReleaseValue(ValueInfo obj) {
        obj.transform.SetParent(this.transform, false);
        valuePool.Release(obj);
    } 
}